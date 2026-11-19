using System.Text.RegularExpressions;
using Districauchos.Core.Vehicles;

namespace Districauchos.Core.Text;

/// <summary>
/// Títulos legibles (propuesta de mejora 5): "MANGUERA BOMBA AGUA CHEVROLET AVEO 1.5CC (IMPORTADORA VCH)"
/// → "Manguera bomba agua Chevrolet Aveo 1.5CC". Solo presentación; la hoja no se toca.
/// </summary>
public sealed partial class TitleCase
{
    /// <summary>Siglas y marcas de fabricante que se escriben en mayúsculas.</summary>
    private static readonly HashSet<string> Acronyms = new(StringComparer.OrdinalIgnoreCase)
    {
        "GM", "VW", "INR", "ABS", "AC", "TD", "TDI", "CRDI", "EFI", "MPFI", "TBI", "SOHC", "DOHC", "LH", "RH",
        "NGK", "SAE", "PCV", "PVC", "OEM", "HB", "SW", "NX", "GT", "GTI", "SZ", "XLT", "XL", "XS", "SX",
        "MGT", "ONN", "AYG", "BHS", "CVT", "ATF", "UNF", "GL", "GLS", "LX", "EX", "SE", "DX", "CC",
        "PK", "PZ", "PZS", "AT", "MT", "HD",
    };

    /// <summary>
    /// Nombres de modelo que también son palabras comunes ("tubo de escape"): solo se capitalizan
    /// dentro de una frase ("Ford Escape", "Fiat Uno"), nunca solos.
    /// </summary>
    private static readonly HashSet<string> CommonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "UNO", "ESCAPE", "DELTA", "SUPER", "RIO", "PREMIO", "BEAT", "SPRINT", "MARCH", "FIT", "RACER",
    };

    private readonly Dictionary<string, string> _proper = new(StringComparer.OrdinalIgnoreCase);
    private readonly Regex? _phrases;
    private readonly Dictionary<string, string> _phraseForms = new(StringComparer.OrdinalIgnoreCase);

    /// <param name="properNames">
    /// Nombres con su forma correcta: marcas, modelos y combinaciones marca + modelo
    /// ("Chevrolet", "Grand Vitara", "D-Max", "Ford Escape"). Los de una palabra se aplican palabra por
    /// palabra; los de varias, solo como frase completa (así "GRAND" suelto no se capitaliza).
    /// </param>
    public TitleCase(IEnumerable<string> properNames)
    {
        foreach (var raw in properNames)
        {
            var name = raw?.Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (name.Contains(' ')) _phraseForms.TryAdd(TextNorm.CollapseSpaces(name), TextNorm.CollapseSpaces(name));
            else if (!CommonWords.Contains(name)) _proper.TryAdd(name, name);
        }
        if (_phraseForms.Count > 0)
            _phrases = new Regex(
                @"(?<![\p{L}\p{N}])(?:" + string.Join('|', _phraseForms.Keys.OrderByDescending(k => k.Length)
                    .Select(k => string.Join(@"\s+", k.Split(' ').Select(Regex.Escape)))) + @")(?![\p{L}\p{N}])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    /// <summary>Marcas, modelos y "marca modelo" del diccionario de vehículos.</summary>
    public static TitleCase From(VehicleDictionary dict) => new(
        dict.Brands.Select(b => b.Name)
            .Concat(dict.Brands.SelectMany(b => b.Models.SelectMany(m => new[] { m.Name, $"{b.Name} {m.Name}" }))));

    public string Format(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var s = SupplierTag().Replace(TextNorm.RepairEncoding(text), " ");
        s = TextNorm.CollapseSpaces(s).Trim().TrimEnd(',', '-', '–', '/', ' ');

        var src = s;
        s = Word().Replace(src, m => FormatWord(m.Value, src, m.Index));
        if (_phrases is not null)
            s = _phrases.Replace(s, m => _phraseForms.TryGetValue(TextNorm.CollapseSpaces(m.Value), out var f) ? f : m.Value);

        // Primera letra en mayúscula
        var first = s.FirstOrDefault(char.IsLetter);
        var i = first == default ? -1 : s.IndexOf(first);
        return i < 0 ? s : s[..i] + char.ToUpperInvariant(s[i]) + s[(i + 1)..];
    }

    private string FormatWord(string word, string all, int index)
    {
        if (_proper.TryGetValue(word, out var proper)) return proper;
        if (word.Any(char.IsDigit)) return word.ToUpperInvariant();                 // 1.5CC, 6PK1875, 4X4, H100
        if (Acronyms.Contains(word) || Roman().IsMatch(word)) return word.ToUpperInvariant(); // INR, Megane II
        if (word.Contains('.'))                                                     // "MULT.ADM.HYUNDAI" → "mult.adm.Hyundai"
        {
            var offset = index;
            return string.Join('.', word.Split('.').Select(part =>
            {
                var r = FormatWord(part, all, offset);
                offset += part.Length + 1;
                return r;
            }));
        }
        if (word.Length == 1)
        {
            // "A/C", "C/U", "S.A.": letra suelta junto a "/" o "." se queda en mayúscula
            var before = index > 0 ? all[index - 1] : ' ';
            var after = index + 1 < all.Length ? all[index + 1] : ' ';
            if (before is '/' or '.' || after is '/' or '.') return word.ToUpperInvariant();
        }
        return word.ToLowerInvariant();
    }

    // Cualquier variante mal escrita: "importadora", "importacion", "impotadora", "imprtadora"… seguida de VCH.
    [GeneratedRegex(@"\(?\s*\b[A-Z]*MP[A-Z]*\s*VCH\b\s*\)?", RegexOptions.IgnoreCase)] private static partial Regex SupplierTag();
    [GeneratedRegex(@"^(?:II|III|IV|VI|VII|VIII|IX)$", RegexOptions.IgnoreCase)] private static partial Regex Roman();
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[-.][\p{L}\p{N}]+)*")] private static partial Regex Word();
}
