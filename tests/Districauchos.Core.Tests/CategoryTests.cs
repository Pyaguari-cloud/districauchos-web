using Districauchos.Core.Catalog;
using Districauchos.Core.Pricing;

namespace Districauchos.Core.Tests;

public class CategoryTests
{
    private const string Json = """
        { "version": "t", "default": "Otros",
          "categories": [
            { "name": "Mangueras", "prefixes": ["MANGUERA", "MAGUERA"] },
            { "name": "Bases de motor y caja", "prefixes": ["BASE MOTOR", "BASE CAJA"] },
            { "name": "Bases y topes de amortiguador", "prefixes": ["BASE AMORTIGUADOR", "CAUCHO BASE AMORTIGUADOR"] },
            { "name": "Bases y soportes", "prefixes": ["BASE", "SOPORTE"] },
            { "name": "Cauchos", "prefixes": ["CAUCHO"] },
            { "name": "Guardapolvos", "prefixes": ["GUARDAPOLVO"] }
          ],
          "from_sheet": { "BUJES": "Bases y soportes", "MANGUERAS": "Mangueras" } }
        """;

    private readonly CategoryClassifier _c = CategoryClassifier.Parse(Json);

    [Theory]
    [InlineData("MANGUERA RADIADOR AVEO", "Mangueras")]
    [InlineData("Maguera calefaccion", "Mangueras")]                       // error de tipeo como prefijo
    [InlineData("BASE MOTOR.LH LUV 2.2", "Bases de motor y caja")]        // punto = espacio
    [InlineData("BASE AMORTIGUADOR POSTERIOR AVEO", "Bases y topes de amortiguador")]
    [InlineData("CAUCHO BASE AMORTIGUADOR AVEO", "Bases y topes de amortiguador")] // prefijo más largo gana a CAUCHO
    [InlineData("BASE ESPIRAL KIA", "Bases y soportes")]                  // BASE genérico
    [InlineData("44133-60A00 Guardapolvo EJE VITARA", "Guardapolvos")]    // salta el código inicial
    [InlineData("CAUCHO BARRA ESTABILIZADORA", "Cauchos")]
    public void Classifies_by_how_the_description_starts(string description, string expected)
    {
        var r = _c.Classify(description, null);
        Assert.Equal(expected, r.Category);
        Assert.Equal(CategorySource.Description, r.Source);
    }

    [Fact]
    public void Brand_first_descriptions_use_first_part_word_found_later()
    {
        var r = _c.Classify("HYUNDAI ELANTRA GUARDAPOLVO PUNTA EJE", null);
        Assert.Equal("Guardapolvos", r.Category);
        Assert.Equal("… GUARDAPOLVO", r.Rule);
    }

    [Fact]
    public void Prefix_must_be_whole_word() =>
        Assert.Equal("Otros", _c.Classify("BASEMENT X", null).Category);

    [Fact]
    public void Falls_back_to_sheet_category_then_default()
    {
        Assert.Equal(new CategoryResult("Mangueras", CategorySource.Sheet, "Mangueras"), _c.Classify("RACOR 12MM", "Mangueras"));
        Assert.Equal(CategorySource.Default, _c.Classify("RACOR 12MM", "Producto Importado").Source);
        Assert.Equal("Otros", _c.Classify(null, null).Category);
    }

    [Fact]
    public void Rejects_sheet_mapping_to_unknown_category() =>
        Assert.Throws<InvalidDataException>(() => CategoryClassifier.Parse("""
            { "version": "t", "categories": [ { "name": "A", "prefixes": ["A"] } ], "from_sheet": { "X": "No existe" } }
            """));

    [Fact]
    public void Builder_uses_classifier_and_keeps_subcategory_only_when_consistent()
    {
        var b = new CatalogBuilder(new PricingRules(), Fixtures.Extractor(), categories: _c);
        var rows = new[]
        {
            Fixtures.Master("C1", "MANGUERA RADIADOR AVEO", 5m, "O1", row: 7) with { Category = "MANGUERAS", Subcategory = "MANGUERAS CODO" },
            Fixtures.Master("C2", "GUARDAPOLVO CAJA KIA", 5m, "O2", row: 8) with { Category = "CAUCHOS", Subcategory = "VARIOS" },
        };
        var r = b.Build(rows, []);

        var manguera = Assert.Single(r.Items, i => i.OemNorm == "O1");
        Assert.Equal(("Mangueras", "MANGUERAS CODO"), (manguera.Category, manguera.Subcategory));

        var guardapolvo = Assert.Single(r.Items, i => i.OemNorm == "O2");
        Assert.Equal(("Guardapolvos", (string?)null), (guardapolvo.Category, guardapolvo.Subcategory));

        Assert.Equal(2, r.CategoryAssignments.Count);
    }

    [Fact]
    public void Real_rules_file_is_valid()
    {
        var path = FindUp("data/categories.json");
        var c = CategoryClassifier.Load(path);
        Assert.Equal("Bases de motor y caja", c.Classify("BASE MOTOR DEL HYUNDAI H1", null).Category);
        Assert.Equal("Mangueras", c.Classify("Manguera Bomba Agua INR Aveo", "PRODUCTO NACIONAL").Category);
    }

    private static string FindUp(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, rel))) return Path.Combine(d.FullName, rel);
        throw new FileNotFoundException(rel);
    }
}
