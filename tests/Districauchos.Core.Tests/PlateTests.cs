using Districauchos.Core.Vehicles;
using Xunit;

namespace Districauchos.Core.Tests;

public class PlateTests
{
    // Diccionario real (data/vehicles.json): la placa trae los nombres como los escribe la ANT.
    private readonly VehicleExtractor _x = new(VehicleDictionary.Load(FindData()));

    private static string FindData()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "data", "vehicles.json"))) return Path.Combine(d.FullName, "data", "vehicles.json");
        throw new FileNotFoundException("data/vehicles.json");
    }

    [Theory]
    [InlineData("PBS-8958", "PBS8958")]
    [InlineData(" pbs 8958 ", "PBS8958")]
    [InlineData("POR-039", "POR0039")]
    [InlineData("IB-123A", "IB123A")]
    [InlineData("PB-8958", null)]
    [InlineData("12345", null)]
    [InlineData("PBSS8958", null)]
    [InlineData("", null)]
    public void Normalizes_ecuador_plates(string raw, string? expected) => Assert.Equal(expected, Plate.Normalize(raw));

    [Fact]
    public void Displays_with_hyphen() => Assert.Equal("PBS-8958", Plate.Display("PBS8958"));

    [Theory]
    [InlineData("8LATW5865B0101950", 2011)]
    [InlineData("9BFZB55X3L8815423", 2020)]
    [InlineData("KNAFC526165414244", 2006)]
    [InlineData("WBXPC93488WJ05373", 2008)]
    [InlineData("CORTO", null)]
    public void Year_from_vin(string vin, int? expected) => Assert.Equal(expected, Plate.YearFromVin(vin, 2027));

    [Theory]
    [InlineData("CHEVROLET", "AVEO EMOTION 1.6L STD", "Chevrolet", "Aveo")]
    [InlineData("FORD", "ECOSPORT AC 2.0L 5P 4X2 TA", "Ford", "EcoSport")]
    [InlineData("KIA", "CARENS 7 PAS 2.0L", "Kia", "Carens")]
    [InlineData("CHEVROLET", "LUV D-MAX 3.0 DIESEL", "Chevrolet", "D-Max")]
    [InlineData("CHEVROLET", "MODELO RARO", "Chevrolet", null)]
    public void Maps_plate_vehicle_to_catalog(string make, string model, string brand, string? catalogModel)
    {
        var m = _x.MatchSingle(make, model);
        Assert.NotNull(m);
        Assert.Equal(brand, m!.Brand);
        Assert.Equal(catalogModel, m.Model);
    }

    [Fact]
    public void Unknown_brand_has_no_match() => Assert.Null(_x.MatchSingle("BMW", "X3 3.0SI"));
}
