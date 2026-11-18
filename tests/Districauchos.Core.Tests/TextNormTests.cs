using Districauchos.Core.Pricing;
using Districauchos.Core.Text;

namespace Districauchos.Core.Tests;

public class TextNormTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")] [InlineData("-")]
    [InlineData("nulo")] [InlineData("NULO")] [InlineData("N/A")]
    public void Empty_tokens_are_empty(string? v) => Assert.Null(TextNorm.Clean(v));

    [Theory]
    [InlineData("$ 12,50", 12.50)]
    [InlineData("$12.50", 12.50)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("-3", -3)]
    [InlineData("7", 7)]
    public void Parses_prices_and_stock(string input, double expected) =>
        Assert.Equal((decimal)expected, TextNorm.ParseDecimal(input));

    [Fact] public void Unparseable_is_null() => Assert.Null(TextNorm.ParseDecimal("abc"));

    [Theory]
    [InlineData(" 96-180.071 ", "96180071")]
    [InlineData("ab 12-3", "AB123")]
    public void Normalizes_oem(string raw, string expected) => Assert.Equal(expected, TextNorm.NormalizeOem(raw));

    [Fact]
    public void Splits_multi_oem_cells()
    {
        Assert.Equal(["96180071", "96180072"], TextNorm.SplitOems("96180071 / 96180072"));
        Assert.Equal(["AAA111", "BBB222"], TextNorm.SplitOems("AAA111, BBB222"));
        Assert.Equal(["96180071", "12345678"], TextNorm.SplitOems("96180071 12345678"));
        // Partes cortas separadas por espacio = un solo OEM escrito con espacios
        Assert.Equal(["96180071"], TextNorm.SplitOems("9618 0071"));
    }

    [Fact]
    public void Codes_keep_leading_zeros_but_match_without_them()
    {
        Assert.Equal("074130790858", TextNorm.NormalizeCode(" 074130790858 "));
        Assert.Equal(TextNorm.CodeMatchKey("74130790858"), TextNorm.CodeMatchKey("074130790858"));
        Assert.Equal("C220", TextNorm.CodeMatchKey("C220"));
    }

    [Fact] public void Repairs_encoding_for_display() => Assert.Equal("PUÑO PUERTA", TextNorm.RepairEncoding("PU?O PUERTA"));

    [Fact]
    public void Header_keys_ignore_accents_case_and_punctuation()
    {
        Assert.Equal(Sheets.HeaderTable.Key("Desc.Sub Categ."), Sheets.HeaderTable.Key("  desc. sub  categ "));
        Assert.Equal(Sheets.HeaderTable.Key("Código de producto"), Sheets.HeaderTable.Key("CODIGO DE PRODUCTO"));
    }

    [Fact]
    public void Slug_has_oem_and_short_title() =>
        Assert.Equal("96180071-manguera-tubo-distribuidor-agua-chevrolet-aveo",
            Slug.ForItem("96180071", "MANGUERA TUBO DISTRIBUIDOR AGUA CHEVROLET AVEO, CHEVYTAXI"));
}

public class PricingTests
{
    private readonly PricingRules _p = new(0.40m, 200m);

    [Theory]
    [InlineData("IMPORTADORA VCH", true)]
    [InlineData("importadora  vch", true)]
    [InlineData("IMPORTADORA VCH CIA LTDA", true)]
    [InlineData("OTRO PROVEEDOR", false)]
    [InlineData(null, false)]
    public void Detects_vch(string? supplier, bool expected) => Assert.Equal(expected, PricingRules.IsVch(supplier));

    [Fact] public void Mayor_is_60_percent_rounded() => Assert.Equal(7.41m, _p.PrecioMayor(12.35m, true));
    [Fact] public void No_mayor_for_non_vch() => Assert.Null(_p.PrecioMayor(10m, false));
    [Fact] public void Override_wins() => Assert.Equal(5.5m, _p.PrecioMayor(10m, true, 5.5m));

    [Fact]
    public void Threshold()
    {
        Assert.False(_p.AppliesMayor(199.99m));
        Assert.True(_p.AppliesMayor(200m));
        Assert.Equal(50m, _p.FaltaParaMayor(150m));
        Assert.Equal(0m, _p.FaltaParaMayor(250m));
    }
}
