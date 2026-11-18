using System.Text;

namespace Districauchos.Sheets;

/// <summary>
/// Lee pestañas exportadas como CSV (Archivo → Descargar → CSV) desde <c>{dir}/{pestaña}.csv</c>.
/// Sirve para desarrollar y probar antes de tener la cuenta de servicio compartida.
/// Detecta automáticamente separador ',' o ';'.
/// </summary>
public sealed class CsvSheetSource(string directory) : ISheetSource
{
    public async Task<IReadOnlyList<IReadOnlyList<string>>> ReadTabAsync(string spreadsheetId, string tab, CancellationToken ct = default)
    {
        var path = Path.Combine(directory, tab + ".csv");
        if (!File.Exists(path)) throw new FileNotFoundException($"Falta el CSV de la pestaña '{tab}'", path);
        var text = await File.ReadAllTextAsync(path, Encoding.UTF8, ct);
        return Parse(text);
    }

    public static IReadOnlyList<IReadOnlyList<string>> Parse(string text)
    {
        // Las primeras filas pueden ser botones/avisos sin separadores: mirar las primeras 20 líneas.
        var head = string.Join('\n', text.Split('\n').Take(20));
        var sep = head.Count(c => c == ';') > head.Count(c => c == ',') ? ';' : ',';

        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var p = 0; p < text.Length; p++)
        {
            var ch = text[p];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (p + 1 < text.Length && text[p + 1] == '"') { cell.Append('"'); p++; }
                    else inQuotes = false;
                }
                else cell.Append(ch);
                continue;
            }

            if (ch == '"') inQuotes = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n')
            {
                row.Add(cell.ToString()); cell.Clear();
                rows.Add(row); row = [];
            }
            else if (ch != '\r' && ch != '﻿') cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        return rows;
    }
}
