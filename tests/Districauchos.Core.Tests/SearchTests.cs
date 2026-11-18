using System.Text.Json;
using Districauchos.Core.Catalog;
using Districauchos.Core.Model;
using Districauchos.Core.Pricing;
using Districauchos.Search;

namespace Districauchos.Core.Tests;

public class SearchTests
{
    private static CatalogBuildResult Catalog() =>
        new CatalogBuilder(new PricingRules(), Fixtures.Extractor()).Build(
            [
                Fixtures.Master("C1", "BUJE CHEVROLET AVEO", 10m, "96180071", "IMPORTADORA VCH", 7),
                Fixtures.Master("C2", "FILTRO KIA", 4m, "F1", null, 8),
            ],
            [
                new StockRow("C1", StockLocation.Tabiazo, 3, "A", 7),
                new StockRow("C2", StockLocation.Tabiazo, 0, null, 8),
            ]);

    [Fact]
    public void Documents_have_public_fields_and_vehicle_combos()
    {
        var docs = ProductIndex.BuildDocuments(Catalog());
        var buje = Assert.Single(docs, d => d.Oem == "96180071");

        Assert.Equal(buje.Slug, buje.Id);
        Assert.Equal(["Chevrolet"], buje.Brands);
        Assert.Equal(["Chevrolet|Aveo"], buje.Vehicles);
        Assert.Equal(1, buje.AbcRank);
        Assert.Equal(1, buje.AvailableRank);
        Assert.Equal(6m, buje.MayorMin);

        var filtro = Assert.Single(docs, d => d.Oem == "F1");
        Assert.Equal(["Kia"], filtro.Brands);
        Assert.Empty(filtro.Vehicles); // solo marca, sin modelo
        Assert.Equal(4, filtro.AbcRank);
        Assert.Equal(0, filtro.AvailableRank);
    }

    [Fact]
    public void Documents_serialize_without_internal_data()
    {
        var json = JsonSerializer.Serialize(ProductIndex.BuildDocuments(Catalog()), MeiliClient.Json);
        Assert.Contains("\"oem_aliases\"", json);
        Assert.DoesNotContain("IMPORTADORA", json.Replace("BUJE CHEVROLET AVEO", ""), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stock", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cost", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("96-180.071", "96180071")]
    [InlineData("C220", "C220")]
    [InlineData("manguera aveo", "manguera aveo")]
    [InlineData("  ", "")]
    [InlineData(null, "")]
    public void Code_like_queries_are_normalized(string? q, string expected) =>
        Assert.Equal(expected, ProductIndex.NormalizeQuery(q));

    [Fact]
    public void Synonyms_are_bidirectional()
    {
        var s = ProductIndex.Synonyms;
        Assert.Contains("chevytaxi", s["aveo"]);
        Assert.Contains("aveo", s["chevytaxi"]);
        Assert.Equal(["chev", "gm"], s["chevrolet"]);
    }
}
