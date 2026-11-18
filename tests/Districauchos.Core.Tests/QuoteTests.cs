using Districauchos.Core.Pricing;

namespace Districauchos.Core.Tests;

public class QuoteTests
{
    private readonly QuoteCalculator _q = new(new PricingRules(0.40m, 200m));

    private static readonly Dictionary<string, IReadOnlyList<QuoteVariant>> Catalog = new()
    {
        // VCH: tiene precio por mayor
        ["96180071"] = [new("96180071", "Manguera tubo distribuidor agua Chevrolet Aveo", "A1", 10.00m, 6.00m, true, 1)],
        // No VCH, dos variantes
        ["0682561"] = [
            new("0682561", "Base motor Chevrolet Corsa", "B1", 17.00m, null, true, 2),
            new("0682561", "Base motor Chevrolet Corsa", "B2", 21.50m, null, false, 2),
        ],
    };

    [Fact]
    public void Below_threshold_uses_public_price_and_says_how_much_is_missing()
    {
        var r = _q.Calculate([new("96180071", null, 3), new("0682561", null, 1)], Catalog);
        Assert.Equal(47.00m, r.SubtotalPvp);
        Assert.Equal(47.00m, r.Total);
        Assert.False(r.MayorApplied);
        Assert.Equal(18.00m, r.BaseMayor);          // solo VCH, a precio por mayor: 3 × 6
        Assert.Equal(182.00m, r.FaltaParaMayor);
        Assert.Contains("Subtotal: $47.00 (precio al por mayor no aplicado)", r.Message);
    }

    [Fact]
    public void Threshold_counts_only_vch_at_mayor_price()
    {
        // $204 a precio público en VCH = $122.40 a precio por mayor → NO llega (respuesta del cliente)
        var r = _q.Calculate([new("96180071", null, 20), new("0682561", null, 20)], Catalog); // VCH 200 PVP / 120 mayor + otros 340
        Assert.False(r.MayorApplied);
        Assert.Equal(80.00m, r.FaltaParaMayor);
    }

    [Fact]
    public void At_threshold_only_vch_lines_get_mayor_price()
    {
        var r = _q.Calculate([new("96180071", null, 34), new("0682561", null, 2)], Catalog); // VCH 34 × 6 = 204
        Assert.True(r.MayorApplied);
        Assert.Equal(0m, r.FaltaParaMayor);
        var vch = r.Lines.Single(l => l.Oem == "96180071");
        Assert.Equal((6.00m, 204.00m, true), (vch.UnitPrice, vch.LineTotal, vch.MayorApplied));
        var other = r.Lines.Single(l => l.Oem == "0682561");
        Assert.Equal((17.00m, false), (other.UnitPrice, other.MayorApplied));
        Assert.Equal(238.00m, r.Total);
        Assert.Equal(136.00m, r.Ahorro);
    }

    [Fact]
    public void Cart_without_vch_never_mentions_mayor()
    {
        var r = _q.Calculate([new("0682561", null, 1)], Catalog);
        Assert.False(r.HasVch);
        Assert.Equal(0m, r.FaltaParaMayor);
        Assert.DoesNotContain("mayor", r.Message);
    }

    [Fact]
    public void Message_follows_brief_format()
    {
        var r = _q.Calculate([new("96180071", null, 2), new("0682561", "B2", 1)], Catalog, "Chevrolet Aveo");
        Assert.Equal("""
            Hola, quiero cotizar:
            1) 96180071 – Manguera tubo distribuidor agua Chevrolet Aveo – Cant: 2 – $10.00 c/u
            2) 0682561 (cód. B2) – Base motor Chevrolet Corsa – Cant: 1 – $21.50 c/u
            Subtotal: $41.50 (precio al por mayor no aplicado)
            Vehículo: Chevrolet Aveo
            Precios con IVA incluido
            Enviado desde districauchosvch.com
            """.ReplaceLineEndings("\n"), r.Message);
    }

    [Fact]
    public void Merges_duplicates_clamps_qty_and_reports_unknown()
    {
        var r = _q.Calculate([
            new("96180071", null, 2), new("96180071", null, 3),
            new("0682561", "B1", -5), new("NOEXISTE", null, 1), new("0682561", "ZZ", 1),
        ], Catalog);
        Assert.Equal(5, r.Lines.Single(l => l.Oem == "96180071").Qty);
        Assert.Equal(1, r.Lines.Single(l => l.Code == "B1").Qty);
        Assert.Equal(["NOEXISTE", "0682561 (ZZ)"], r.Missing);
    }

    [Fact]
    public void Limits_lines_and_quantity()
    {
        var many = Enumerable.Range(0, 80).Select(i => new QuoteRequestLine($"X{i}", null, 1));
        Assert.Equal(QuoteCalculator.MaxLines, _q.Calculate(many, Catalog).Missing.Count);
        Assert.Equal(QuoteCalculator.MaxQty, _q.Calculate([new("96180071", null, 1_000_000)], Catalog).Lines[0].Qty);
    }
}
