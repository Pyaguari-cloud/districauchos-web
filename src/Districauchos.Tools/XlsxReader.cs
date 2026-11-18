using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Districauchos.Tools;

/// <summary>
/// Lector mínimo de .xlsx (primera hoja, valores ya calculados). Sin dependencias externas:
/// solo se usa para el Excel de precios sugeridos, que el cliente pasa de vez en cuando.
/// </summary>
internal static partial class XlsxReader
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    /// <summary>Filas como (número de fila, columna → texto). Solo devuelve las columnas pedidas.</summary>
    public static IEnumerable<(int Row, Dictionary<string, string> Cells)> ReadFirstSheet(string path, Func<string, bool>? keepColumn = null)
    {
        using var zip = ZipFile.OpenRead(path);
        var shared = zip.GetEntry("xl/sharedStrings.xml") is { } ss
            ? XDocument.Load(ss.Open()).Root!.Elements(Ns + "si").Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value))).ToList()
            : [];
        var sheet = zip.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidDataException("El Excel no tiene hoja 1");
        var doc = XDocument.Load(sheet.Open());

        foreach (var row in doc.Descendants(Ns + "row"))
        {
            var cells = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var c in row.Elements(Ns + "c"))
            {
                var col = ColumnOf().Match((string?)c.Attribute("r") ?? "").Value;
                if (col.Length == 0 || (keepColumn is not null && !keepColumn(col))) continue;
                var type = (string?)c.Attribute("t");
                var raw = type == "inlineStr" ? string.Concat(c.Descendants(Ns + "t").Select(t => t.Value)) : c.Element(Ns + "v")?.Value;
                if (raw is null) continue;
                cells[col] = type == "s" && int.TryParse(raw, out var i) && i < shared.Count ? shared[i] : raw;
            }
            yield return ((int?)row.Attribute("r") ?? 0, cells);
        }
    }

    [GeneratedRegex("^[A-Z]+")] private static partial Regex ColumnOf();
}
