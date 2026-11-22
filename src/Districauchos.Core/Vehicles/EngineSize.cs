using System.Text.RegularExpressions;
using Districauchos.Core.Text;

namespace Districauchos.Core.Vehicles;

/// <summary>
/// Cilindraje escrito en la descripción: "1.4", "1,6L", "2.0CC", "3.0 TD" → "1.4", "1.6", "2.0", "3.0".
/// No cuenta medidas ("1.5\"", "2.5MM", "1.5 X 20", "12.5", "60X1.5") ni voltajes ("1.5V"). Rango 0.8–6.9.
/// </summary>
public static partial class EngineSize
{
    public readonly record struct Hit(int Index, int Length, string Value);

    /// <summary>Sobre texto ya en mayúsculas y sin tildes (como lo usa <see cref="VehicleExtractor"/>).</summary>
    public static IEnumerable<Hit> Find(string upper)
    {
        foreach (Match m in Pattern().Matches(upper))
        {
            var value = $"{m.Groups[1].Value}.{m.Groups[2].Value}";
            if (value is "0.0" or "0.1" or "0.2" or "0.3" or "0.4" or "0.5" or "0.6" or "0.7") continue;
            yield return new Hit(m.Index, m.Length, value);
        }
    }

    /// <summary>Cilindrajes de un texto libre (columna "Motor" de la hoja: "1.4 / 1.6", "2.0 16V").</summary>
    public static IReadOnlyList<string> Parse(string? text) =>
        string.IsNullOrWhiteSpace(text) ? []
        : Find(TextNorm.StripAccents(text).ToUpperInvariant()).Select(h => h.Value).Distinct().ToList();

    [GeneratedRegex("""(?<![0-9.,X])([0-6])[.,]([0-9])(?![0-9.,]|\s*(?:MM|CM|M\b|MTS?\b|"|''|PULG|X\b|X[0-9]|KG|AMP|A\b|V\b|W\b|%))""")]
    private static partial Regex Pattern();
}
