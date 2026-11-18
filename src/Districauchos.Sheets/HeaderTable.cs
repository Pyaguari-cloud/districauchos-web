using Districauchos.Core.Text;

namespace Districauchos.Sheets;

/// <summary>
/// Acceso a columnas por nombre de encabezado (nunca por posición, brief §5).
/// La clave de un encabezado es solo letras y dígitos en minúscula sin tildes:
/// "Desc.Sub Categ." → "descsubcateg", "OEM (codigo de parte )" → "oemcodigodeparte".
/// Admite encabezados repetidos (p. ej. dos "Stock actual").
/// </summary>
public sealed class HeaderTable
{
    private readonly Dictionary<string, List<int>> _columns = new(StringComparer.Ordinal);

    /// <summary>Índice 0-based de la fila de encabezados dentro de la pestaña.</summary>
    public int HeaderRowIndex { get; }

    public IReadOnlyList<string> RawHeaders { get; }

    private HeaderTable(IReadOnlyList<string> headers, int headerRowIndex)
    {
        HeaderRowIndex = headerRowIndex;
        RawHeaders = headers;
        for (var i = 0; i < headers.Count; i++)
        {
            var key = Key(headers[i]);
            if (key.Length == 0) continue;
            if (!_columns.TryGetValue(key, out var list)) _columns[key] = list = [];
            list.Add(i);
        }
    }

    public static string Key(string? header) =>
        TextNorm.NormalizeForMatch(header).Replace(" ", "").ToLowerInvariant();

    /// <summary>
    /// Busca la fila de encabezados: primero en <paramref name="preferredRowIndex"/> (fila 6 → índice 5),
    /// si no, en las primeras 20 filas, la primera que contenga <paramref name="requiredHeader"/>.
    /// </summary>
    public static HeaderTable Locate(IReadOnlyList<IReadOnlyList<string>> rows, string requiredHeader, int preferredRowIndex = 5)
    {
        var required = Key(requiredHeader);
        bool Has(int i) => i < rows.Count && rows[i].Any(c => Key(c) == required);

        if (Has(preferredRowIndex)) return new HeaderTable(rows[preferredRowIndex], preferredRowIndex);
        for (var i = 0; i < Math.Min(20, rows.Count); i++)
            if (Has(i)) return new HeaderTable(rows[i], i);

        throw new InvalidDataException($"No se encontró el encabezado '{requiredHeader}' en las primeras 20 filas");
    }

    /// <summary>Índice de columna para el primer candidato que exista, o null.</summary>
    public int? Find(int occurrence, params string[] candidates)
    {
        foreach (var c in candidates)
            if (_columns.TryGetValue(Key(c), out var list) && list.Count >= occurrence)
                return list[occurrence - 1];
        return null;
    }

    public int? Find(params string[] candidates) => Find(1, candidates);

    public int Count(string header) => _columns.TryGetValue(Key(header), out var l) ? l.Count : 0;

    /// <summary>
    /// Columnas cuyo encabezado empieza con <paramref name="prefix"/>, de izquierda a derecha.
    /// Ej.: "Stock actual" encuentra "Stock actual", "STOCK ACTUAL 1" y "Stock Actual ".
    /// </summary>
    public IReadOnlyList<int> FindByPrefix(string prefix)
    {
        var p = Key(prefix);
        return _columns.Where(kv => kv.Key.StartsWith(p, StringComparison.Ordinal))
            .SelectMany(kv => kv.Value).Order().ToList();
    }

    public static string? Cell(IReadOnlyList<string> row, int? col) =>
        col is { } c && c < row.Count ? row[c] : null;
}
