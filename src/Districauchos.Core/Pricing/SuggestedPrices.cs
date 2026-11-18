using Districauchos.Core.Text;

namespace Districauchos.Core.Pricing;

/// <summary>Fila del Excel "costos y precios sugeridos". Solo ID y precio por mayor: el costo nunca se lee.</summary>
public sealed record SuggestedPriceRow(int Row, string Id, string? PrecioMayorText);

/// <summary>Ficha publicada contra la que se empareja (por OEM o por código de variante).</summary>
public sealed record SuggestedPriceTarget(string Oem, bool IsVch, decimal PvpMin);

public enum SuggestedPriceStatus { Applied, NoPrice, InvalidPrice, NotFound, NotVch, NotBelowPvp }

public sealed record SuggestedPriceResult(
    int Row, string Id, string? Oem, decimal? PrecioMayor, decimal? Pvp, SuggestedPriceStatus Status);

/// <summary>
/// Respuesta del cliente (2026-11-18): productos del Excel → precio por mayor del Excel; resto VCH → 40 %.
/// Se guarda en price_overrides. Nunca se acepta un precio por mayor igual o mayor al público.
/// </summary>
public static class SuggestedPrices
{
    public static IReadOnlyList<SuggestedPriceResult> Match(
        IEnumerable<SuggestedPriceRow> rows,
        IReadOnlyDictionary<string, SuggestedPriceTarget> byOem,
        IReadOnlyDictionary<string, string> oemByCodeKey)
    {
        // Comparación "suelta" (solo letras y números): en la hoja hay OEM con apóstrofo en vez de guion
        // ("48674'26040") que el Excel escribe "48674-26040". No se cambia la normalización general (cambiaría URLs).
        var loose = new Dictionary<string, SuggestedPriceTarget>(StringComparer.Ordinal);
        foreach (var t in byOem.Values) loose.TryAdd(Loose(t.Oem), t);
        var looseCodes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (code, oem) in oemByCodeKey) looseCodes.TryAdd(Loose(code), oem);

        var results = new List<SuggestedPriceResult>();
        foreach (var row in rows)
        {
            var target = Find(row.Id, byOem, oemByCodeKey) ?? FindLoose(row.Id, loose, looseCodes, byOem);
            if (string.IsNullOrWhiteSpace(row.PrecioMayorText))
            {
                results.Add(new(row.Row, row.Id, target?.Oem, null, target?.PvpMin, SuggestedPriceStatus.NoPrice));
                continue;
            }
            var price = TextNorm.ParseDecimal(row.PrecioMayorText);
            var status = price is not > 0 ? SuggestedPriceStatus.InvalidPrice
                : target is null ? SuggestedPriceStatus.NotFound
                : !target.IsVch ? SuggestedPriceStatus.NotVch
                : price >= target.PvpMin ? SuggestedPriceStatus.NotBelowPvp
                : SuggestedPriceStatus.Applied;
            results.Add(new(row.Row, row.Id, target?.Oem, price is null ? null : Math.Round(price.Value, 2), target?.PvpMin, status));
        }

        // Si dos filas apuntan a la misma ficha, gana la primera (como en la hoja principal).
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return results.Select(r => r.Status == SuggestedPriceStatus.Applied && !seen.Add(r.Oem!)
            ? r with { Status = SuggestedPriceStatus.InvalidPrice } : r).ToList();
    }

    private static string[] SplitIds(string id) =>
        id.Split(['/', '\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Loose(string s) => new(s.ToUpperInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());

    private static SuggestedPriceTarget? FindLoose(string id, Dictionary<string, SuggestedPriceTarget> loose,
        Dictionary<string, string> looseCodes, IReadOnlyDictionary<string, SuggestedPriceTarget> byOem)
    {
        foreach (var part in SplitIds(id))
        {
            var k = Loose(part);
            if (k.Length < 5) continue; // evita coincidencias por casualidad con códigos cortos
            if (loose.TryGetValue(k, out var t)) return t;
            if (looseCodes.TryGetValue(k, out var oem) && byOem.TryGetValue(oem, out var t2)) return t2;
        }
        return null;
    }

    /// <summary>El ID puede ser un OEM, un código, o varios separados por "/" o saltos de línea.</summary>
    private static SuggestedPriceTarget? Find(
        string id, IReadOnlyDictionary<string, SuggestedPriceTarget> byOem, IReadOnlyDictionary<string, string> oemByCodeKey)
    {
        foreach (var part in SplitIds(id))
        {
            if (TextNorm.NormalizeOem(part) is { } oem && byOem.TryGetValue(oem, out var t)) return t;
            if (TextNorm.CodeMatchKey(TextNorm.NormalizeCode(part)) is { } key
                && oemByCodeKey.TryGetValue(key, out var viaCode) && byOem.TryGetValue(viaCode, out var t2)) return t2;
        }
        return null;
    }
}
