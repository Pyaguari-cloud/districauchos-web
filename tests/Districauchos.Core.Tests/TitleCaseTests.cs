using Districauchos.Core.Text;

namespace Districauchos.Core.Tests;

public class TitleCaseTests
{
    private readonly TitleCase _t = new([
        "Chevrolet", "Suzuki", "Ford", "Fiat", "Mercedes-Benz", "Great Wall",
        "Aveo", "Corsa", "Luv", "D-Max", "BT-50", "Grand Vitara", "Vitara", "H100", "Santa Fe",
        "Escape", "Ford Escape", "Uno", "Fiat Uno",
    ]);

    [Theory]
    [InlineData("MANGUERA BOMBA AGUA CHEVROLET AVEO 1.5CC (IMPORTADORA VCH)", "Manguera bomba agua Chevrolet Aveo 1.5CC")]
    [InlineData("BASE MOTOR Chevrolet CORSA", "Base motor Chevrolet Corsa")]
    [InlineData("BUJE BARRA D-MAX LUV 4X4", "Buje barra D-Max Luv 4X4")]
    [InlineData("SOPORTE GRAND VITARA", "Soporte Grand Vitara")]
    [InlineData("TAPA GRAND", "Tapa grand")]                                   // "GRAND" suelto no es nombre
    [InlineData("TUBO DE ESCAPE UNIVERSAL", "Tubo de escape universal")]       // palabra común
    [InlineData("BASE MOTOR FORD ESCAPE", "Base motor Ford Escape")]           // como frase sí
    [InlineData("FILTRO A/C HYUNDAI H100", "Filtro A/C hyundai H100")]         // Hyundai no está en esta lista de prueba
    [InlineData("6PK1875 BANDA INR", "6PK1875 banda INR")]
    [InlineData("44133-60A00 guardapolvo EJE VITARA", "44133-60A00 guardapolvo eje Vitara")]
    [InlineData("BASE CAJA CLIO III- KANGO II", "Base caja clio III- kango II")]
    [InlineData("MANGUERA .MULT.ADM.CHEVROLET AVEO", "Manguera .mult.adm.Chevrolet Aveo")]
    [InlineData("caucho  puerta  ,", "Caucho puerta")]
    [InlineData("PIÑON CAÑERIA MAZDA BT-50", "Piñon cañeria mazda BT-50")]
    public void Formats_titles(string input, string expected) => Assert.Equal(expected, _t.Format(input));

    [Fact]
    public void Empty_is_empty() => Assert.Equal("", _t.Format("  "));

    [Fact]
    public void Real_dictionary_names_apply()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "data/vehicles.json"))) dir = dir.Parent;
        var t = TitleCase.From(Vehicles.VehicleDictionary.Load(Path.Combine(dir!.FullName, "data/vehicles.json")));
        Assert.Equal("Base amortiguador Hyundai Santa Fe 2.4", t.Format("BASE AMORTIGUADOR HYUNDAI SANTA FE 2.4"));
        Assert.Equal("Buje meseta Great Wall Wingle", t.Format("BUJE MESETA GREAT WALL WINGLE"));
    }
}
