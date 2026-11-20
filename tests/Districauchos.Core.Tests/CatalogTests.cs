using Districauchos.Core.Catalog;
using Districauchos.Core.Model;
using Districauchos.Core.Pricing;
using Districauchos.Core.Text;
using Districauchos.Core.Vehicles;

namespace Districauchos.Core.Tests;

internal static class Fixtures
{
    public const string VehiclesJson = """
        { "version": "test", "brands": [
          { "name": "Chevrolet", "aliases": ["CHEVROLET","CHEV","GM"], "models": [
              { "name": "Aveo", "aliases": ["AVEO","AVEO EMOTION","CHEVYTAXI"] },
              { "name": "D-Max", "aliases": ["D MAX","DMAX"] },
              { "name": "Vitara", "aliases": ["VITARA"] } ] },
          { "name": "Suzuki", "aliases": ["SUZUKI"], "models": [ { "name": "Vitara", "aliases": ["VITARA"] } ] },
          { "name": "Kia", "aliases": ["KIA"], "models": [ { "name": "Rio", "aliases": ["RIO"] } ] }
        ] }
        """;

    public static VehicleExtractor Extractor() => new(VehicleDictionary.Parse(VehiclesJson));

    public static MasterRow Master(string code, string? desc, decimal? pvp, string? oem, string? supplier = null, int row = 7) =>
        new(code, TextNorm.NormalizeCode(code)!, desc, pvp, "MOTOR", null, oem, TextNorm.SplitOems(oem), supplier, null, row);
}

public class VehicleExtractorTests
{
    private readonly VehicleExtractor _x = Fixtures.Extractor();

    [Fact]
    public void Model_without_brand_infers_brand()
    {
        var r = _x.Extract("MANGUERA TUBO DISTRIBUIDOR AGUA AVEO, CHEVYTAXI");
        Assert.Equal([new VehicleMatch("Chevrolet", "Aveo")], r.Matches);
    }

    [Fact]
    public void Matches_whole_words_only() =>
        Assert.Empty(_x.Extract("TORNILLO PERIODICO").Matches); // contiene "RIO" dentro de una palabra

    [Fact]
    public void Hyphenated_aliases_match() =>
        Assert.Contains(new VehicleMatch("Chevrolet", "D-Max"), _x.Extract("KIT EMBRAGUE D-MAX 3.0").Matches);

    [Fact]
    public void Multi_brand_model_uses_brand_in_text_or_all_brands()
    {
        Assert.Equal([new VehicleMatch("Suzuki", "Vitara")], _x.Extract("BOMBA AGUA SUZUKI VITARA").Matches);
        var amb = _x.Extract("BOMBA AGUA VITARA");
        Assert.Equal(
            new[] { new VehicleMatch("Chevrolet", "Vitara"), new VehicleMatch("Suzuki", "Vitara") }.ToHashSet(),
            amb.Matches.ToHashSet());
        Assert.Equal(["Vitara"], amb.AmbiguousModels);
    }

    [Fact]
    public void Brand_only_is_kept() =>
        Assert.Equal([new VehicleMatch("Kia", null)], _x.Extract("FILTRO ACEITE KIA").Matches);

    [Fact]
    public void Several_vehicles() =>
        Assert.Equal(2, _x.Extract("PASTILLAS FRENO AVEO / KIA RIO").Matches.Count);

    [Fact]
    public void Equivalent_models_are_assigned_to_all_twin_brands()
    {
        var x = new VehicleExtractor(VehicleDictionary.Parse("""
            { "version": "t",
              "brands": [
                { "name": "Chevrolet", "aliases": ["CHEVROLET"], "models": [
                    { "name": "Grand Vitara", "aliases": ["GRAND VITARA"] }, { "name": "Aveo", "aliases": ["AVEO"] } ] },
                { "name": "Suzuki", "aliases": ["SUZUKI"], "models": [ { "name": "Grand Vitara", "aliases": ["GRAND VITARA"] } ] } ],
              "equivalents": [ { "brands": ["Chevrolet", "Suzuki"], "models": ["Grand Vitara"] } ] }
            """));

        var both = new[] { new VehicleMatch("Chevrolet", "Grand Vitara"), new VehicleMatch("Suzuki", "Grand Vitara") }.ToHashSet();
        Assert.Equal(both, x.Extract("BOMBA AGUA CHEVROLET GRAND VITARA").Matches.ToHashSet());
        Assert.Equal(both, x.Extract("BOMBA AGUA SUZUKI GRAND VITARA").Matches.ToHashSet());
        // Solo los modelos del grupo: un Aveo de Chevrolet no pasa a Suzuki
        Assert.Equal([new VehicleMatch("Chevrolet", "Aveo")], x.Extract("FILTRO CHEVROLET AVEO").Matches);
    }

    [Fact]
    public void Model_name_is_not_an_alias_when_aliases_are_listed()
    {
        var x = new VehicleExtractor(VehicleDictionary.Parse("""
            { "version": "t", "brands": [ { "name": "Ford", "aliases": ["FORD"], "models": [
                { "name": "Escape", "aliases": ["FORD ESCAPE"] } ] } ] }
            """));
        Assert.Empty(x.Extract("EMPAQUE TUBO ESCAPE NISSAN").Matches);
        Assert.Contains(new VehicleMatch("Ford", "Escape"), x.Extract("SOPORTE MOTOR FORD ESCAPE").Matches);
    }

    [Fact]
    public void Real_dictionary_parses()
    {
        var path = FindUp("data/vehicles.json");
        var dict = VehicleDictionary.Load(path);
        Assert.NotEmpty(dict.Brands);
        Assert.Contains(new VehicleMatch("Chevrolet", "Aveo"), new VehicleExtractor(dict).Extract("ROTULA CHEVYTAXI").Matches);
    }

    private static string FindUp(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, rel))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }
}

public class CatalogBuilderTests
{
    private readonly CatalogBuilder _b = new(new PricingRules(), Fixtures.Extractor());

    [Fact]
    public void Excludes_generic_no_description_and_no_price()
    {
        var r = _b.Build([
            Fixtures.Master("50", "VENTA RAPIDA", 1m, "X1"),
            Fixtures.Master("51", "VENTA RAPIDA", 1m, "X2"),
            Fixtures.Master("A1", null, 5m, "X3"),
            Fixtures.Master("A2", "SIN PRECIO", 0m, "X4"),
            Fixtures.Master("96180071", "TIENE OEM COMO CODIGO PERO OEM VACIO", 5m, null),
            Fixtures.Master("A3", "OK AVEO", 5m, "X5"),
        ], []);

        Assert.Equal(2, r.Items.Count); // A3 (OEM X5) y 96180071 (sin OEM → ficha por código)
        Assert.Equal(2, r.Stats.ExcludedGeneric);
        Assert.Equal(1, r.Stats.ExcludedNoDescription);
        Assert.Equal(1, r.Stats.ExcludedNoPrice);
        Assert.Equal(1, r.Stats.WithoutOem);
        Assert.Equal(1, r.Stats.ProductsWithOem);
    }

    [Fact]
    public void Rows_without_oem_become_their_own_item_keyed_by_code()
    {
        // Caso real: "ALZAS CAUCHO DE CABINA" (DH'40) no tiene OEM y sí existencias.
        var r = _b.Build(
            [
                Fixtures.Master("DH'40", "ALZAS CAUCHO DE CABINA UNIVERSALES 51X10X26", 3m, null, row: 7),
                Fixtures.Master("DH'50", "ALZAS CAUCHO DE CABINA UNIVERSALES 62X14X25", 3m, null, row: 8),
                Fixtures.Master("A3", "BASE CABINA DMAX", 9m, "8980513280", row: 9),
            ],
            [new StockRow("DH'40", StockLocation.Tabiazo, 4, null, 7)]);

        Assert.Equal(3, r.Items.Count); // no se agrupan entre sí aunque se parezcan
        var dh40 = Assert.Single(r.Items, i => i.OemNorm == "COD:DH'40");
        Assert.True(CatalogKey.IsCode(dh40.OemNorm));
        Assert.Null(CatalogKey.Oem(dh40.OemNorm));
        Assert.Empty(dh40.OemAliases);
        Assert.Equal(["DH'40"], dh40.Codes);
        Assert.True(dh40.Available);
        Assert.Equal("cod-dh-40-alzas-caucho-de-cabina-universales-51x10x26", dh40.Slug);
        Assert.False(Assert.Single(r.Items, i => i.OemNorm == "COD:DH'50").Available);

        // La ficha con OEM va primero y conserva su URL de siempre.
        Assert.Equal("8980513280", r.Items[0].OemNorm);
        Assert.Equal("8980513280-base-cabina-dmax", r.Items[0].Slug);
        Assert.Contains(r.Variants, v => v.OemNorm == "COD:DH'40" && v.Code == "DH'40");
    }

    [Theory]
    [InlineData("96180071", "C1", false, "96180071")]
    [InlineData("96180071", "C1", true, "96180071 (cód. C1)")]
    [InlineData("COD:DH'40", "DH'40", false, "cód. DH'40")]
    [InlineData("COD:DH'40", "DH'40", true, "cód. DH'40")]
    public void Catalog_key_label_for_people(string key, string code, bool showCode, string expected) =>
        Assert.Equal(expected, CatalogKey.Label(key, code, showCode));

    [Fact]
    public void Groups_by_oem_and_sums_stock_of_all_locations()
    {
        var r = _b.Build(
            [
                Fixtures.Master("C1", "BUJE AVEO", 10m, "96-180.071", "IMPORTADORA  VCH", 7),
                Fixtures.Master("C2", "BUJE AVEO GENERICO", 8m, "96180071", "OTRO", 8),
                Fixtures.Master("C3", "FILTRO KIA RIO", 4m, "F1", null, 9),
            ],
            [
                new StockRow("C1", StockLocation.Tabiazo, -5, "B", 7),
                new StockRow("C1", StockLocation.Bodega, 0, null, 7),
                new StockRow("C2", StockLocation.Cusubamba, -2, null, 8),
                new StockRow("C2", StockLocation.Maldonado, 3, null, 8),
                new StockRow("C3", StockLocation.Tabiazo, -1, "A", 9),
            ]);

        var buje = Assert.Single(r.Items, i => i.OemNorm == "96180071");
        Assert.Equal(["C1", "C2"], buje.Codes);
        Assert.True(buje.Available); // C2: -2 Cusubamba + 3 Maldonado = 1
        Assert.Equal(1m, buje.Stock); // C1: -5 + 0 → 0 (nunca negativo)
        Assert.True(buje.IsVch);
        Assert.Equal(8m, buje.PvpMin);
        Assert.Equal(10m, buje.PvpMax);
        Assert.Equal(6m, buje.MayorMin);
        Assert.Equal("BUJE AVEO GENERICO", buje.Title); // variante disponible primero
        Assert.Equal("B", buje.AbcClass);

        var c1 = Assert.Single(r.Variants, v => v.Code == "C1");
        Assert.False(c1.Available);
        Assert.Equal(0m, c1.Stock);
        Assert.Equal(6m, c1.PrecioMayor);
        Assert.Null(Assert.Single(r.Variants, v => v.Code == "C2").PrecioMayor);

        var filtro = Assert.Single(r.Items, i => i.OemNorm == "F1");
        Assert.False(filtro.Available);
        Assert.Contains(new ProductVehicle("F1", "Kia", "Rio"), r.Vehicles);
    }

    [Fact]
    public void Negative_in_one_store_cancels_positive_in_another()
    {
        // Caso real del POS: Maldonado -6, Tabiazo +4 → en realidad no quedan; se muestra "Bajo pedido".
        var r = _b.Build([Fixtures.Master("14000", "BASE CABINA LUV-DMAX ALTA", 11m, "8980513280")],
            [new StockRow("14000", StockLocation.Maldonado, -6, null, 7), new StockRow("14000", StockLocation.Tabiazo, 4, null, 7)]);
        Assert.False(r.Items[0].Available);
        Assert.Equal(0m, r.Items[0].Stock);
    }

    [Fact]
    public void Matches_stock_codes_ignoring_leading_zeros()
    {
        var r = _b.Build([Fixtures.Master("074130790858", "ACEITE", 20m, "OEM1")],
            [new StockRow("74130790858", StockLocation.Tabiazo, 2, null, 7)]);
        Assert.True(r.Items[0].Available);
    }

    [Fact]
    public void Duplicate_codes_keep_first_row()
    {
        var r = _b.Build([Fixtures.Master("C1", "PRIMERA", 5m, "O1", row: 7), Fixtures.Master("C1", "SEGUNDA", 6m, "O1", row: 8)], []);
        Assert.Equal("PRIMERA", Assert.Single(r.Items).Title);
        Assert.Equal(1, r.Stats.DuplicateCodes);
    }

    [Fact]
    public void Price_override_replaces_mayor()
    {
        var r = _b.Build([Fixtures.Master("C1", "X", 10m, "O1", "IMPORTADORA VCH")], [],
            new Dictionary<string, PriceOverride> { ["O1"] = new("O1", 4.25m, null, "excel") });
        Assert.Equal(4.25m, r.Items[0].MayorMin);
    }

    [Fact]
    public void Items_without_vehicle_are_listed_for_review()
    {
        var r = _b.Build([Fixtures.Master("C1", "TORNILLO 10MM", 1m, "O1")], []);
        Assert.Equal("O1", Assert.Single(r.WithoutVehicle).OemNorm);
    }
}
