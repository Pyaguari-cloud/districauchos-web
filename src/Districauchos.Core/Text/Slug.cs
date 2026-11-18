using System.Text.RegularExpressions;

namespace Districauchos.Core.Text;

public static partial class Slug
{
    /// <summary>Slug de ficha: {oem}-{titulo-corto} (máx. 6 palabras del título).</summary>
    public static string ForItem(string oemNorm, string title, int maxWords = 6)
    {
        // Sin la marca del proveedor en la URL ("(IMPORTADORA VCH)" está en 262 títulos, a veces mal escrita).
        var clean = SupplierTag().Replace(TextNorm.NormalizeForMatch(title), " ");
        var words = clean
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(maxWords);
        // El OEM puede traer caracteres raros (p. ej. "48609'87402"): en la URL solo [a-z0-9-].
        var oem = NonSlug().Replace(TextNorm.StripAccents(oemNorm).ToLowerInvariant(), "-");
        var slug = $"{oem}-{string.Join('-', words)}";
        return Dashes().Replace(slug, "-").Trim('-');
    }

    [GeneratedRegex("-{2,}")] private static partial Regex Dashes();
    [GeneratedRegex("[^a-z0-9]+")] private static partial Regex NonSlug();
    [GeneratedRegex(@"\bI?MPORTADORA ?VCH\b")] private static partial Regex SupplierTag();
}
