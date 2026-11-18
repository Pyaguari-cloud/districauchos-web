using System.Globalization;
using System.Text;

namespace Districauchos.Core.Pricing;

/// <summary>Línea pedida por el cliente (carrito). <c>Code</c> = variante elegida; null = la principal.</summary>
public sealed record QuoteRequestLine(string Oem, string? Code, int Qty);

/// <summary>Datos de catálogo de una variante, tal como están publicados.</summary>
public sealed record QuoteVariant(string Oem, string Title, string Code, decimal Pvp, decimal? PrecioMayor, bool Available, int VariantCount);

public sealed record QuoteLine(
    string Oem, string Code, string Title, int Qty, decimal Pvp, decimal UnitPrice, bool MayorApplied,
    decimal LineTotal, bool Available, bool ShowCode);

public sealed record Quote(
    IReadOnlyList<QuoteLine> Lines, IReadOnlyList<string> Missing,
    decimal SubtotalPvp, decimal Total, bool HasVch, bool MayorApplied, decimal FaltaParaMayor, decimal Ahorro,
    decimal Umbral, string Message);

/// <summary>
/// Cotización del carrito (brief §6.4 y §6.7). La calcula siempre el servidor con los precios publicados:
/// el navegador solo manda OEM, variante y cantidad.
/// </summary>
public sealed class QuoteCalculator(PricingRules pricing, string site = "districauchosvch.com")
{
    public const int MaxLines = 50;
    public const int MaxQty = 999;

    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");
    public static string Money(decimal v) => v.ToString("$#,##0.00", Us);

    /// <param name="variantsByOem">Variantes publicadas por OEM, la principal primero.</param>
    public Quote Calculate(
        IEnumerable<QuoteRequestLine> request, IReadOnlyDictionary<string, IReadOnlyList<QuoteVariant>> variantsByOem,
        string? vehicle = null)
    {
        // Misma ficha y variante pedida dos veces → una sola línea con la suma.
        var merged = request
            .Where(l => !string.IsNullOrWhiteSpace(l.Oem))
            .GroupBy(l => (Oem: l.Oem.Trim(), Code: string.IsNullOrWhiteSpace(l.Code) ? null : l.Code.Trim()))
            .Select(g => (g.Key.Oem, g.Key.Code, Qty: Math.Clamp(g.Sum(x => (long)x.Qty), 1, MaxQty)))
            .Take(MaxLines)
            .ToList();

        var missing = new List<string>();
        var chosen = new List<(QuoteVariant V, int Qty)>();
        foreach (var (oem, code, qty) in merged)
        {
            if (!variantsByOem.TryGetValue(oem, out var variants) || variants.Count == 0) { missing.Add(oem); continue; }
            var v = code is null ? variants[0] : variants.FirstOrDefault(x => x.Code == code);
            if (v is null) { missing.Add($"{oem} ({code})"); continue; }
            chosen.Add((v, (int)qty));
        }

        // Supuesto pendiente 12.1: el umbral se mide sobre todo el carrito a precio público.
        var subtotalPvp = chosen.Sum(c => c.V.Pvp * c.Qty);
        var applies = pricing.AppliesMayor(subtotalPvp);
        var lines = chosen.Select(c =>
        {
            var mayor = applies && c.V.PrecioMayor is not null;
            var unit = mayor ? c.V.PrecioMayor!.Value : c.V.Pvp;
            return new QuoteLine(c.V.Oem, c.V.Code, c.V.Title, c.Qty, c.V.Pvp, unit, mayor, unit * c.Qty, c.V.Available, c.V.VariantCount > 1);
        }).ToList();

        var total = lines.Sum(l => l.LineTotal);
        var hasVch = chosen.Any(c => c.V.PrecioMayor is not null);
        var falta = hasVch ? pricing.FaltaParaMayor(subtotalPvp) : 0m;
        var mayorApplied = lines.Any(l => l.MayorApplied);

        return new Quote(lines, missing, subtotalPvp, total, hasVch, mayorApplied, falta, subtotalPvp - total,
            pricing.MayorUmbral, BuildMessage(lines, total, hasVch, mayorApplied, vehicle));
    }

    /// <summary>Formato del brief §6.7.</summary>
    private string BuildMessage(IReadOnlyList<QuoteLine> lines, decimal total, bool hasVch, bool mayorApplied, string? vehicle)
    {
        var sb = new StringBuilder("Hola, quiero cotizar:\n");
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            var id = l.ShowCode ? $"{l.Oem} (cód. {l.Code})" : l.Oem;
            sb.Append($"{i + 1}) {id} – {l.Title} – Cant: {l.Qty} – {Money(l.UnitPrice)} c/u");
            if (l.MayorApplied) sb.Append(" (por mayor)");
            sb.Append('\n');
        }
        sb.Append($"Subtotal: {Money(total)}");
        if (hasVch) sb.Append(mayorApplied ? " (precio al por mayor aplicado)" : " (precio al por mayor no aplicado)");
        sb.Append('\n');
        if (!string.IsNullOrWhiteSpace(vehicle)) sb.Append($"Vehículo: {vehicle.Trim()}\n");
        sb.Append($"Enviado desde {site}");
        return sb.ToString();
    }
}
