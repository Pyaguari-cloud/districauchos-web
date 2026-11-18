using Districauchos.Core.Text;

namespace Districauchos.Core.Vehicles;

public sealed record VehicleMatch(string Brand, string? Model);

public sealed record ExtractionResult(IReadOnlyList<VehicleMatch> Matches, IReadOnlyList<string> AmbiguousModels);

/// <summary>
/// Extrae marcas/modelos de la descripción (brief §6.5): coincidencia por palabra completa
/// sobre el texto normalizado. Un modelo sin marca toma la marca del diccionario.
/// Si el modelo existe en varias marcas y ninguna aparece en el texto, se asigna a todas
/// y se informa en AmbiguousModels (para el reporte "modelos multimarca").
/// </summary>
public sealed class VehicleExtractor
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

    private static bool ContainsWord(string paddedText, string alias) =>
        paddedText.Contains(" " + alias + " ", StringComparison.Ordinal);
}
