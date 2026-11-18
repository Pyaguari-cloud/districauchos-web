using Districauchos.Core.Pricing;
using Districauchos.Core.Text;
using S = Districauchos.Core.Pricing.SuggestedPriceStatus;

namespace Districauchos.Core.Tests;

public class SuggestedPricesTests
{
    private static readonly Dictionary<string, SuggestedPriceTarget> ByOem = new()
    {
        [TextNorm.NormalizeOem("55275-3W000")!] = new(TextNorm.NormalizeOem("55275-3W000")!, true, 20m),
        ["96653239"] = new("96653239", true, 10m),
        ["NOVCH1"] = new("NOVCH1", false, 10m),
        ["48674'26040"] = new("48674'26040", true, 30m),
    };
    private static readonly Dictionary<string, string> ByCode = new() { ["ABC77"] = "96653239" };

    private static SuggestedPriceResult One(string id, string? price) =>
        SuggestedPrices.Match([new(5, id, price)], ByOem, ByCode).Single();

    [Fact] public void Matches_by_oem_with_dashes() => Assert.Equal(S.Applied, One("55275-3W000", "11.50").Status);
    [Fact] public void Matches_by_variant_code() => Assert.Equal("96653239", One("abc77", "5").Oem);
    [Fact] public void Matches_any_of_several_ids() => Assert.Equal(S.Applied, One("XXXX/96653239/", "5").Status);
    [Fact] public void Matches_oem_written_with_apostrophe_in_sheet() => Assert.Equal("48674'26040", One("48674-26040", "12").Oem);
    [Fact] public void Rejects_price_not_below_public() => Assert.Equal(S.NotBelowPvp, One("96653239", "10.00").Status);
    [Fact] public void Reports_non_vch_separately() => Assert.Equal(S.NotVch, One("NOVCH1", "5").Status);
    [Fact] public void Reports_not_found() => Assert.Equal(S.NotFound, One("ZZZ", "5").Status);
    [Fact] public void Empty_price_is_ignored() => Assert.Equal(S.NoPrice, One("96653239", " ").Status);
    [Fact] public void Garbage_price_is_invalid() => Assert.Equal(S.InvalidPrice, One("96653239", "consultar").Status);

    [Fact]
    public void First_row_wins_on_duplicates()
    {
        var r = SuggestedPrices.Match([new(5, "96653239", "5"), new(6, "ABC77", "4")], ByOem, ByCode);
        Assert.Equal([S.Applied, S.InvalidPrice], r.Select(x => x.Status));
    }

    [Fact]
    public void Pricing_ignores_override_not_below_public()
    {
        var p = new PricingRules();
        Assert.Equal(6.00m, p.PrecioMayor(10m, true, 12m));   // override inválido → 40 %
        Assert.Equal(7.50m, p.PrecioMayor(10m, true, 7.5m));
    }
}
