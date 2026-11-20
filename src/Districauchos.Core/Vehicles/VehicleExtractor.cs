using System.Text.RegularExpressions;
using Districauchos.Core.Text;

namespace Districauchos.Core.Vehicles;

public sealed record VehicleMatch(string Brand, string? Model);

/// <summary>Rango de años escrito en la descripción para ese vehículo ("Optra 1.8 07/12" → 2007–2012).</summary>
public sealed record VehicleYears(string Brand, string Model, int From, int To);

public sealed record ExtractionResult(IReadOnlyList<VehicleMatch> Matches, IReadOnlyList<string> AmbiguousModels);

/// <summary>
/// Extrae marcas/modelos de la descripción (brief §6.5): coincidencia por palabra completa
/// sobre el texto normalizado. Un modelo sin marca toma la marca del diccionario.
/// Si el modelo existe en varias marcas y ninguna aparece en el texto, se asigna a todas
/// y se informa en AmbiguousModels (para el reporte "modelos multimarca").
/// </summary>
public sealed partial class VehicleExtractor
{
    private readonly List<(string Alias, string Brand)> _brandAliases = [];
    private readonly List<(string Alias, string Brand, string Model)> _modelAliases = [];

    // (marca, modelo) → otras marcas del mismo vehículo
    private readonly Dictionary<(string Brand, string Model), List<string>> _equivalents = [];

    public string DictionaryVersion { get; }

    public VehicleExtractor(VehicleDictionary dict)
    {
        DictionaryVersion = dict.Version;
        foreach (var g in dict.Equivalents)
            foreach (var model in g.Models)
                foreach (var brand in g.Brands)
                    _equivalents[(brand, model)] = g.Brands.Where(b => b != brand).ToList();
        foreach (var b in dict.Brands)
        {
            foreach (var a in VehicleDictionary.NormalizedAliases(b.Name, b.Aliases))
                _brandAliases.Add((a, b.Name));
            foreach (var m in b.Models)
                foreach (var a in VehicleDictionary.NormalizedAliases(m.Name, m.Aliases))
                    _modelAliases.Add((a, b.Name, m.Name));
        }
        // Alias largos primero (no cambia el resultado, pero deja el orden de reporte estable).
        _modelAliases.Sort((x, y) => y.Alias.Length.CompareTo(x.Alias.Length));
    }

    public ExtractionResult Extract(string? description)
    {
        var text = " " + TextNorm.NormalizeForMatch(description) + " ";
        if (text.Trim().Length == 0) return new([], []);

        var brandsInText = _brandAliases
            .Where(b => ContainsWord(text, b.Alias))
            .Select(b => b.Brand)
            .ToHashSet(StringComparer.Ordinal);

        // modelo → marcas candidatas
        var modelHits = _modelAliases
            .Where(m => ContainsWord(text, m.Alias))
            .GroupBy(m => m.Model, StringComparer.Ordinal)
            .ToList();

        var matches = new List<VehicleMatch>();
        var ambiguous = new List<string>();
        var brandsWithModel = new HashSet<string>(StringComparer.Ordinal);

        foreach (var hit in modelHits)
        {
            var candidates = hit.Select(h => h.Brand).Distinct().ToList();
            var chosen = candidates.Count == 1
                ? candidates
                : candidates.Where(brandsInText.Contains).ToList();

            // Modelo de varias marcas sin marca en el texto (Vitara, Grand Vitara, Forza):
            // decisión del cliente 2026-11-18 → se asigna a todas las marcas del diccionario.
            if (chosen.Count == 0)
            {
                ambiguous.Add(hit.Key);
                chosen = candidates;
            }
            // Equivalentes (decisión del cliente 2026-11-18): "Chevrolet Grand Vitara" también es Suzuki Grand Vitara.
            var withTwins = chosen
                .Concat(chosen.SelectMany(b => _equivalents.GetValueOrDefault((b, hit.Key)) ?? []))
                .Distinct();
            foreach (var brand in withTwins)
            {
                matches.Add(new VehicleMatch(brand, hit.Key));
                brandsWithModel.Add(brand);
            }
        }

        // Marca mencionada sin ningún modelo reconocido → se guarda solo la marca.
        foreach (var brand in brandsInText.Where(b => !brandsWithModel.Contains(b)))
            matches.Add(new VehicleMatch(brand, null));

        return new(matches.Distinct().ToList(), ambiguous);
    }

    /// <summary>
    /// Años escritos en la descripción, asignados al modelo (o modelos) mencionado justo antes:
    /// "Mazda B2200 85/98 - B2600 85/98", "Accent/Rio 11/17" (ambos), "Hilux 2.5 11-15", "Corolla 2004-2008".
    /// Solo rangos (dos años separados por "-" o "/"); un año suelto no se toma, para no ocultar repuestos por error.
    /// Las medidas (36X18X45, 1.4-1.6) no cuentan: son otra forma de token o de un dígito.
    /// </summary>
    public IReadOnlyList<VehicleYears> ExtractYears(string? description, int maxYear)
    {
        if (string.IsNullOrWhiteSpace(description)) return [];
        var s = TextNorm.StripAccents(TextNorm.RepairEncoding(description)).ToUpperInvariant();
        var toks = Token().Matches(s).ToList();
        if (toks.Count < 2) return [];
        string Sep(int i) => s[(toks[i].Index + toks[i].Length)..toks[i + 1].Index];
        bool Digits(int i) => i >= 0 && i < toks.Count && toks[i].Value.All(char.IsAsciiDigit);

        // Marcas de cada modelo según la extracción normal (incluye gemelos tipo Chevrolet/Suzuki).
        var brandsOf = Extract(description).Matches.Where(m => m.Model is not null)
            .GroupBy(m => m.Model!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(m => m.Brand).ToList(), StringComparer.Ordinal);
        if (brandsOf.Count == 0) return [];
        var aliasToks = _modelAliases.Where(a => brandsOf.ContainsKey(a.Model))
            .Select(a => (Toks: a.Alias.Split(' '), a.Model)).ToList(); // ya ordenados: largos primero

        var result = new List<VehicleYears>();
        var pending = new List<string>();
        var last = new List<string>(); // modelos del último rango: "Hilux Fortuner 12-14 SR5 05-12" → ambos rangos
        var i = 0;
        while (i < toks.Count)
        {
            var hit = aliasToks.FirstOrDefault(a => i + a.Toks.Length <= toks.Count &&
                a.Toks.Select((t, k) => toks[i + k].Value == t).All(x => x));
            if (hit.Toks is not null)
            {
                if (!pending.Contains(hit.Model)) pending.Add(hit.Model);
                i += hit.Toks.Length;
                continue;
            }
            if ((pending.Count > 0 || last.Count > 0) && Digits(i) && Digits(i + 1) && RangeSep().IsMatch(Sep(i))
                // "40/16/41" o "12-14-16" son medidas, no años.
                && !(i + 2 < toks.Count && Digits(i + 2) && RangeSep().IsMatch(Sep(i + 1)))
                && !(i > 0 && Digits(i - 1) && RangeSep().IsMatch(Sep(i - 1)))
                && Year(toks[i].Value, maxYear) is { } from && Year(toks[i + 1].Value, maxYear) is { } to
                && from <= to && to - from <= 45)
            {
                if (pending.Count > 0) { last = [.. pending]; pending.Clear(); }
                foreach (var model in last)
                    foreach (var brand in brandsOf[model])
                        result.Add(new VehicleYears(brand, model, from, to));
                i += 2;
                continue;
            }
            i++;
        }
        return result.Distinct().ToList();
    }

    /// <summary>"07" → 2007, "85" → 1985, "2004" → 2004. Fuera de 1960..maxYear → null.</summary>
    private static int? Year(string t, int maxYear)
    {
        if (t.Length is not (2 or 4)) return null;
        var v = int.Parse(t, System.Globalization.CultureInfo.InvariantCulture);
        if (t.Length == 2) v = 2000 + v > maxYear ? 1900 + v : 2000 + v;
        return v >= 1960 && v <= maxYear ? v : null;
    }

    [GeneratedRegex("[A-Z0-9]+")] private static partial Regex Token();
    [GeneratedRegex(@"^\s*[-/]\s*$")] private static partial Regex RangeSep();

    private static bool ContainsWord(string paddedText, string alias) =>
        paddedText.Contains(" " + alias + " ", StringComparison.Ordinal);
}
