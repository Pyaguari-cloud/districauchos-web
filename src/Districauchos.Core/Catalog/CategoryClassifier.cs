using System.Text.Json;
using System.Text.Json.Serialization;
using Districauchos.Core.Text;

namespace Districauchos.Core.Catalog;

public enum CategorySource { Description, Sheet, Default }

public sealed record CategoryResult(string Category, CategorySource Source, string? Rule);

/// <summary>
/// Categoría del catálogo según cómo empieza la descripción (propuesta de mejora 2).
/// Reglas en data/categories.json. Solo afecta la presentación; la hoja no se modifica.
/// </summary>
public sealed class CategoryClassifier
{
    private sealed class Config
    {
        [JsonPropertyName("version")] public string Version { get; init; } = "";
        [JsonPropertyName("default")] public string Default { get; init; } = "Otros";
        [JsonPropertyName("categories")] public List<Entry> Categories { get; init; } = [];
        [JsonPropertyName("from_sheet")] public Dictionary<string, string> FromSheet { get; init; } = [];
    }

    private sealed class Entry
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("prefixes")] public List<string> Prefixes { get; init; } = [];
    }

    // (prefijo normalizado, categoría), del más largo al más corto
    private readonly List<(string Prefix, string Category)> _prefixes;
    private readonly Dictionary<string, string> _fromSheet;
    private readonly string _default;

    public string Version { get; }
    public IReadOnlyList<string> Categories { get; }

    private CategoryClassifier(Config c)
    {
        Version = c.Version;
        _default = c.Default;
        Categories = c.Categories.Select(x => x.Name).Append(c.Default).Distinct().ToList();
        _prefixes = c.Categories
            .SelectMany(e => e.Prefixes.Select(p => (Prefix: TextNorm.NormalizeForMatch(p), Category: e.Name)))
            .Where(p => p.Prefix.Length > 0)
            .OrderByDescending(p => p.Prefix.Length)
            .ToList();
        _fromSheet = c.FromSheet.ToDictionary(kv => TextNorm.NormalizeForMatch(kv.Key), kv => kv.Value, StringComparer.Ordinal);

        var unknown = _fromSheet.Values.Where(v => !Categories.Contains(v)).Distinct().ToList();
        if (unknown.Count > 0)
            throw new InvalidDataException($"categories.json: 'from_sheet' apunta a categorías inexistentes: {string.Join(", ", unknown)}");
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static CategoryClassifier Parse(string json) =>
        new(JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? throw new InvalidDataException("categories.json vacío"));

    public static CategoryClassifier Load(string path) => Parse(File.ReadAllText(path));

    public CategoryResult Classify(string? description, string? sheetCategory)
    {
        var head = Head(description);
        if (MatchAt(head) is { } atStart)
            return new(atStart.Category, CategorySource.Description, atStart.Prefix);

        // Descripción que empieza por la marca/modelo ("HYUNDAI ELANTRA BOCIN PLATO…"):
        // se toma la primera palabra de pieza que aparezca más adelante.
        var tokens = head.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 1; i < tokens.Length; i++)
            if (MatchAt(string.Join(' ', tokens[i..])) is { } later)
                return new(later.Category, CategorySource.Description, "… " + later.Prefix);

        var sheet = TextNorm.NormalizeForMatch(sheetCategory);
        if (sheet.Length > 0 && _fromSheet.TryGetValue(sheet, out var mapped))
            return new(mapped, CategorySource.Sheet, sheetCategory);

        return new(_default, CategorySource.Default, null);
    }

    private (string Prefix, string Category)? MatchAt(string text)
    {
        if (text.Length == 0) return null;
        foreach (var p in _prefixes) // ya ordenados del más largo al más corto
            if (text == p.Prefix || text.StartsWith(p.Prefix + " ", StringComparison.Ordinal))
                return p;
        return null;
    }

    /// <summary>
    /// Descripción normalizada desde la primera palabra "real": salta códigos iniciales
    /// ("44133-60A00 Guardapolvo EJE" → "GUARDAPOLVO EJE"; "96322618 BARRA" → "BARRA").
    /// </summary>
    internal static string Head(string? description)
    {
        var tokens = TextNorm.NormalizeForMatch(description).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var start = Array.FindIndex(tokens, t => t.Length >= 2 && t.All(char.IsAsciiLetter));
        return start < 0 ? "" : string.Join(' ', tokens[start..]);
    }
}
