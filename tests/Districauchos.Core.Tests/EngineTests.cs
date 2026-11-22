using Districauchos.Core.Vehicles;
using Xunit;

namespace Districauchos.Core.Tests;

public class EngineTests
{
    private readonly VehicleExtractor _x = Fixtures.Extractor();

    private List<string> Engines(string d) =>
        _x.ExtractEngines(d).Select(e => $"{e.Brand} {e.Model} {e.Engine}").OrderBy(s => s).ToList();

    [Theory]
    [InlineData("1.4", "1.4")]
    [InlineData("motor 1,6L", "1.6")]
    [InlineData("Tucson 2.0CC 16V", "2.0")]
    [InlineData("D-Max 3.0 TD", "3.0")]
    [InlineData("1.4 / 1.6", "1.4,1.6")]
    [InlineData("Aveo 1.4-1.6", "1.4,1.6")]
    [InlineData("Aveo 1.4/1.6", "1.4,1.6")]
    [InlineData("manguera 1.5\" x 1 m", "")]
    [InlineData("tubo 2.5MM", "")]
    [InlineData("medida 1.5 X 20", "")]
    [InlineData("60X1.5", "")]
    [InlineData("12.5 pulgadas", "")]
    [InlineData("bateria 1.5V", "")]
    [InlineData("0.5", "")]
    [InlineData("Alto 0.8", "0.8")]
    public void Parses_engine_sizes(string text, string expected) =>
        Assert.Equal(expected, string.Join(",", EngineSize.Parse(text)));

    [Fact]
    public void Assigns_engine_to_the_model_before_it()
    {
        Assert.Equal(["Chevrolet Aveo 1.4"], Engines("Manguera radiador inferior Aveo 1.4 07/12"));
        Assert.Equal(["Chevrolet Aveo 1.4", "Chevrolet Aveo 1.6"], Engines("Banda Aveo 1.4 1.6"));
        Assert.Equal(["Chevrolet Aveo 1.4", "Chevrolet Aveo 1.6"], Engines("Manguera calef sal Chevrolet Aveo 1.4/1.6"));
        Assert.Contains("Kia Rio 1.4", Engines("Banda 4PK610 Kia Rio 1.4L / Aveo 1.6"));
        Assert.DoesNotContain("Kia Rio 1.6", Engines("Banda 4PK610 Kia Rio 1.4L / Aveo 1.6"));
        Assert.Contains("Chevrolet Aveo 1.6", Engines("Banda 4PK610 Kia Rio 1.4L / Aveo 1.6"));
        Assert.Empty(Engines("1.6 manguera sin vehiculo"));
        Assert.Empty(Engines("Buje mesa Aveo 60X12X46"));
    }
}
