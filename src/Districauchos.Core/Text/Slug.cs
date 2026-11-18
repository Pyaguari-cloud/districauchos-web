using System.Text.RegularExpressions;

namespace Districauchos.Core.Text;

public static partial class Slug
{
    /// <summary>Slug de ficha: {oem}-{titulo-corto} (máx. 6 palabras del título).</summary>
    public static string ForItem(string oemNorm, string title, int maxWords = 6)
    {
        var words = TextNorm.NormalizeForMatch(title)
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Take(maxWords);
        var slug = $"{oemNorm.ToLowerInvariant()}-{string.Join('-', words)}";
        return Dashes().Replace(slug, "-").Trim('-');
    }

    [GeneratedRegex("-{2,}")] private static partial Regex Dashes();
}
