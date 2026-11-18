using System.Text.Json;
using System.Text.Json.Serialization;
using Districauchos.Core.Text;

namespace Districauchos.Core.Vehicles;

/// <summary>Diccionario versionado marca → modelos → alias (data/vehicles.json).</summary>
public sealed class VehicleDictionary
{
    [JsonPropertyName("version")] public string Version { get; init; } = "";
    [JsonPropertyName("brands")] public List<BrandEntry> Brands { get; init; } = [];

    /// <summary>
    /// Modelos que son el mismo vehículo con distinta marca (p. ej. Chevrolet/Suzuki Grand Vitara).
    /// Un repuesto de uno se asigna también a los demás, diga o no la marca la descripción.
    /// </summary>
    [JsonPropertyName("equivalents")] public List<EquivalentGroup> Equivalents { get; init; } = [];

    public sealed class EquivalentGroup
    {
        [JsonPropertyName("brands")] public List<string> Brands { get; init; } = [];
        [JsonPropertyName("models")] public List<string> Models { get; init; } = [];
    }

    public sealed class BrandEntry
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("aliases")] public List<string> Aliases { get; init; } = [];
        [JsonPropertyName("models")] public List<ModelEntry> Models { get; init; } = [];
    }

    public sealed class ModelEntry
    {
        [JsonPropertyName("name")] public string Name { get; init; } = "";
        [JsonPropertyName("aliases")] public List<string> Aliases { get; init; } = [];
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static VehicleDictionary Load(string path) =>
        Parse(File.ReadAllText(path));

    public static VehicleDictionary Parse(string json) =>
        JsonSerializer.Deserialize<VehicleDictionary>(json, JsonOptions)
        ?? throw new InvalidDataException("vehicles.json vacío o inválido");

    /// <summary>
    /// Alias normalizados. Si la lista de alias tiene valores, se usa SOLO esa lista
    /// (así un nombre que es palabra común, como "Escape" o "Uno", no se busca suelto).
    /// Si está vacía, el nombre sirve como alias.
    /// </summary>
    internal static IEnumerable<string> NormalizedAliases(string name, IReadOnlyCollection<string> aliases) =>
        (aliases.Count > 0 ? aliases : [name])
            .Select(TextNorm.NormalizeForMatch)
            .Where(a => a.Length > 0)
            .Distinct();
}
