using System.Text.RegularExpressions;

namespace Districauchos.Core.Vehicles;

/// <summary>Placas de Ecuador para la búsqueda por placa.</summary>
public static partial class Plate
{
    /// <summary>
    /// "pbs-8958" → "PBS8958"; las antiguas de 3 números llevan ahora un 0 adelante ("POR-039" → "POR0039");
    /// motos "IB-123A" → "IB123A". null si no tiene forma de placa de Ecuador.
    /// </summary>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 20) return null;
        var t = new string(raw.Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        if (Car().IsMatch(t)) return t.Length == 6 ? t[..3] + "0" + t[3..] : t;
        return Moto().IsMatch(t) ? t : null;
    }

    /// <summary>"PBS8958" → "PBS-8958" (como se ve en la placa).</summary>
    public static string Display(string normalized) =>
        Car().IsMatch(normalized) ? $"{normalized[..3]}-{normalized[3..]}" : normalized;

    /// <summary>
    /// Año de modelo según el 10.º carácter del chasis (VIN). El código se repite cada 30 años: se toma el más
    /// reciente que no pase de <paramref name="maxYear"/>. Solo de respaldo si el proveedor no trae el año.
    /// </summary>
    public static int? YearFromVin(string? vin, int maxYear)
    {
        if (vin is not { Length: 17 }) return null;
        const string codes = "ABCDEFGHJKLMNPRSTVWXY123456789"; // 1980 … 2009, luego 2010 … 2039
        var i = codes.IndexOf(char.ToUpperInvariant(vin[9]));
        if (i < 0) return null;
        var year = 1980 + i;
        while (year + 30 <= maxYear) year += 30;
        return year;
    }

    [GeneratedRegex("^[A-Z]{3}[0-9]{3,4}$")] private static partial Regex Car();
    [GeneratedRegex("^[A-Z]{2}[0-9]{3}[A-Z]$")] private static partial Regex Moto();
}
