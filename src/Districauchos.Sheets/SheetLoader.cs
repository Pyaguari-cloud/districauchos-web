using Districauchos.Core;
using Districauchos.Core.Model;
using Districauchos.Core.Text;

namespace Districauchos.Sheets;

public sealed class SheetLoadResult
{
    public List<MasterRow> Master { get; } = [];
    public List<StockRow> Stock { get; } = [];
    public List<CompatSheetRow> Compat { get; } = [];
    public List<SyncWarning> Warnings { get; } = [];
    /// <summary>Filas de datos leídas por pestaña (para sync_runs.rows_read).</summary>
    public Dictionary<string, int> RowsRead { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Lee las 4 fuentes del brief §5 y devuelve filas limpias.
/// No aplica reglas de negocio de publicación (eso es CatalogBuilder).
/// </summary>
public sealed class SheetLoader(ISheetSource source, AppSettings settings)
{
    public const string TabMaster = "Codigos Existentes";
    public const string TabTabiazo = "InventarioTabiazo";
    public const string TabCusubamba = "InventarioCusubamba";
    public const string TabMaldonado = "InventarioMaldonado";
    public const string TabBodega = "Maestro Bodega";
    /// <summary>Opcional, en la hoja principal: compatibilidades verificadas por el vendedor (fase 5).</summary>
    public const string TabCompat = "Compatibilidades";

    public async Task<SheetLoadResult> LoadAsync(CancellationToken ct = default)
    {
        var principal = Require(settings.SheetIdPrincipal, "SHEET_ID_PRINCIPAL");
        var maldonado = Require(settings.SheetIdMaldonado, "SHEET_ID_MALDONADO");
        var bodega = Require(settings.SheetIdBodega, "SHEET_ID_BODEGA");

        var result = new SheetLoadResult();
        LoadMaster(await source.ReadTabAsync(principal, TabMaster, ct), result);
        LoadStore(await source.ReadTabAsync(principal, TabTabiazo, ct), TabTabiazo, StockLocation.Tabiazo, result);
        LoadStore(await source.ReadTabAsync(principal, TabCusubamba, ct), TabCusubamba, StockLocation.Cusubamba, result);
        LoadStore(await source.ReadTabAsync(maldonado, TabMaldonado, ct), TabMaldonado, StockLocation.Maldonado, result);
        LoadBodega(await source.ReadTabAsync(bodega, TabBodega, ct), result);
        await LoadCompatAsync(principal, result, ct);
        return result;
    }

    /// <summary>La pestaña es opcional: si no existe (o no se puede leer), la sincronización sigue igual con un aviso.</summary>
    private async Task LoadCompatAsync(string spreadsheetId, SheetLoadResult result, CancellationToken ct)
    {
        IReadOnlyList<IReadOnlyList<string>> rows;
        try { rows = await source.ReadTabAsync(spreadsheetId, TabCompat, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            result.Warnings.Add(new("compat_tab_missing", $"No se leyó la pestaña '{TabCompat}' (opcional): {e.Message.Split('\n')[0]}", TabCompat));
            return;
        }
        LoadCompat(rows, result);
    }

    internal static void LoadCompat(IReadOnlyList<IReadOnlyList<string>> rows, SheetLoadResult result)
    {
        if (rows.Count == 0) return;
        HeaderTable h;
        try { h = HeaderTable.Locate(rows, "OEM", preferredRowIndex: 0); }
        catch (InvalidDataException) { result.Warnings.Add(new("compat_no_header", "La pestaña no tiene la columna OEM", TabCompat)); return; }
        var cOem = h.Find("OEM");
        var cBrand = h.Find("Marca");
        var cModel = h.Find("Modelo");
        var cEngine = h.Find("Motor");
        var cFrom = h.Find("Año desde", "Ano desde", "Desde");
        var cTo = h.Find("Año hasta", "Ano hasta", "Hasta");
        var cUse = h.Find("Usar", "Verificado");
        MissingColumns(result, TabCompat, ("Marca", cBrand), ("Modelo", cModel), ("Usar", cUse));
        if (cBrand is null || cModel is null || cUse is null) return;

        var read = 0;
        for (var i = h.HeaderRowIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(TextNorm.IsEmpty)) continue;
            read++;
            // Solo lo que el vendedor (o la revisión automática) marcó "Sí".
            var use = TextNorm.StripAccents(HeaderTable.Cell(row, cUse) ?? "").Trim().ToLowerInvariant();
            if (!use.StartsWith('s')) continue;
            var oem = TextNorm.NormalizeOem(HeaderTable.Cell(row, cOem));
            var model = TextNorm.Clean(HeaderTable.Cell(row, cModel));
            if (oem is null || model is null) continue;
            int? Year(int? col)
            {
                var v = TextNorm.Clean(HeaderTable.Cell(row, col));
                return v is not null && v.Length == 4 && int.TryParse(v, out var y) && y is >= 1960 and <= 2100 ? y : null;
            }
            var from = Year(cFrom);
            var to = Year(cTo);
            if (from is not null && to is not null && to < from) (from, to) = (to, from);
            // "Chevrolet / Suzuki" = una fila por marca.
            foreach (var brand in (TextNorm.Clean(HeaderTable.Cell(row, cBrand)) ?? "").Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                result.Compat.Add(new(oem, brand, model, TextNorm.Clean(HeaderTable.Cell(row, cEngine)) ?? "", from, to, i + 1));
        }
        result.RowsRead[TabCompat] = read;
    }

    /// <summary>
    /// Solo las 4 pestañas de stock, en paralelo (revisión al pagar). No lee el maestro ni precios.
    /// </summary>
    public async Task<IReadOnlyList<StockRow>> LoadStockAsync(CancellationToken ct = default)
    {
        var principal = Require(settings.SheetIdPrincipal, "SHEET_ID_PRINCIPAL");
        var maldonado = Require(settings.SheetIdMaldonado, "SHEET_ID_MALDONADO");
        var bodega = Require(settings.SheetIdBodega, "SHEET_ID_BODEGA");

        var tabiazo = source.ReadTabAsync(principal, TabTabiazo, ct);
        var cusubamba = source.ReadTabAsync(principal, TabCusubamba, ct);
        var mal = source.ReadTabAsync(maldonado, TabMaldonado, ct);
        var bod = source.ReadTabAsync(bodega, TabBodega, ct);
        await Task.WhenAll(tabiazo, cusubamba, mal, bod);

        var result = new SheetLoadResult();
        LoadStore(await tabiazo, TabTabiazo, StockLocation.Tabiazo, result);
        LoadStore(await cusubamba, TabCusubamba, StockLocation.Cusubamba, result);
        LoadStore(await mal, TabMaldonado, StockLocation.Maldonado, result);
        LoadBodega(await bod, result);
        return result.Stock;
    }

    private string Require(string? value, string name) =>
        !string.IsNullOrWhiteSpace(value) || settings.LocalCsvDir is not null
            ? value ?? "local"
            : throw new InvalidOperationException($"Falta la variable {name}");

    internal static void LoadMaster(IReadOnlyList<IReadOnlyList<string>> rows, SheetLoadResult result)
    {
        var h = HeaderTable.Locate(rows, "Codigo Producto");
        var cCode = h.Find("Codigo Producto");
        var cDesc = h.Find("Descripcion");
        var cPvp = h.Find("Precio venta $", "Precio venta");
        var cCat = h.Find("Descr. Categoria", "Categoria");
        var cSub = h.Find("Desc.Sub Categ.", "Sub Categoria", "Subcategoria");
        var cOem = h.Find("OEM");
        var cSup = h.Find("PROVEEDOR");
        var cAlt = h.Find("CODIGOS ALTERNATIVOS");
        // "precio de COSTO" se ignora a propósito: nunca entra al pipeline público.

        MissingColumns(result, TabMaster, ("Descripcion", cDesc), ("Precio venta $", cPvp), ("OEM", cOem), ("PROVEEDOR", cSup));

        var read = 0;
        for (var i = h.HeaderRowIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(TextNorm.IsEmpty)) continue;
            read++;
            var sheetRow = i + 1;

            var code = TextNorm.Clean(HeaderTable.Cell(row, cCode));
            var codeNorm = TextNorm.NormalizeCode(code);
            if (code is null || codeNorm is null)
            {
                result.Warnings.Add(new("missing_code", "Fila sin código", TabMaster, sheetRow));
                continue;
            }

            var oemRaw = TextNorm.Clean(HeaderTable.Cell(row, cOem));
            result.Master.Add(new MasterRow(
                Code: code,
                CodeNorm: codeNorm,
                Description: TextNorm.Clean(HeaderTable.Cell(row, cDesc)),
                Pvp: TextNorm.ParseDecimal(HeaderTable.Cell(row, cPvp)),
                Category: TextNorm.Clean(HeaderTable.Cell(row, cCat)),
                Subcategory: TextNorm.Clean(HeaderTable.Cell(row, cSub)),
                OemRaw: oemRaw,
                Oems: TextNorm.SplitOems(oemRaw),
                Supplier: TextNorm.Clean(HeaderTable.Cell(row, cSup)),
                AltCode: TextNorm.Clean(HeaderTable.Cell(row, cAlt)),
                SourceRow: sheetRow));
        }
        result.RowsRead[TabMaster] = read;
    }

    private void LoadStore(IReadOnlyList<IReadOnlyList<string>> rows, string tab, StockLocation loc, SheetLoadResult result) =>
        LoadStore(rows, tab, loc, result, settings.StockColumnOccurrence);

    internal static void LoadStore(IReadOnlyList<IReadOnlyList<string>> rows, string tab, StockLocation loc,
        SheetLoadResult result, int stockOccurrence)
    {
        var h = HeaderTable.Locate(rows, "Codigo Producto");
        var cCode = h.Find("Codigo Producto");
        // Por prefijo: el encabezado real es "CLASIFICACION ABC (A= ALTA ROTACION - B = ...)".
        var cAbc = h.FindByPrefix("CLASIFICACION ABC") is [var abcCol, ..] ? abcCol : (int?)null;

        // Pregunta 12.4 (respondida 2026-11-18): la correcta es la columna J = la primera "Stock actual…".
        // Por prefijo porque en Cusubamba la J se llama "STOCK ACTUAL 1".
        var stockCols = h.FindByPrefix("Stock actual");
        int? cStock = stockCols.Count == 0 ? null : stockCols[Math.Min(stockOccurrence, stockCols.Count) - 1];
        if (stockOccurrence > stockCols.Count && stockCols.Count > 0)
            result.Warnings.Add(new("stock_column_fallback",
                $"Se pidió la columna 'Stock actual' n.º {stockOccurrence} pero solo hay {stockCols.Count}; se usa la primera", tab));
        MissingColumns(result, tab, ("Stock actual", cStock));

        // Diagnóstico: cuántas filas difieren entre las dos columnas "Stock actual".
        int? cOther = stockCols.Count >= 2 ? stockCols[stockOccurrence == 1 ? 1 : 0] : null;
        var differ = 0;

        var read = 0;
        for (var i = h.HeaderRowIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(TextNorm.IsEmpty)) continue;
            read++;
            var codeNorm = TextNorm.NormalizeCode(HeaderTable.Cell(row, cCode));
            if (codeNorm is null) continue;

            var stock = TextNorm.ParseDecimal(HeaderTable.Cell(row, cStock)) ?? 0m;
            if (cOther is not null && (TextNorm.ParseDecimal(HeaderTable.Cell(row, cOther)) ?? 0m) != stock) differ++;

            result.Stock.Add(new StockRow(codeNorm, loc, stock, ParseAbc(HeaderTable.Cell(row, cAbc)), i + 1));
        }
        result.RowsRead[tab] = read;

        if (differ > 0)
            result.Warnings.Add(new("stock_columns_differ",
                $"{differ} filas tienen distinto valor en las dos columnas 'Stock actual' (se usa la n.º {stockOccurrence})", tab));
    }

    internal static void LoadBodega(IReadOnlyList<IReadOnlyList<string>> rows, SheetLoadResult result)
    {
        var h = HeaderTable.Locate(rows, "Codigo de producto");
        var cCode = h.Find("Codigo de producto", "Codigo Producto");
        var cStock = h.Find("STOCK ACTUAL"); // confirmado por el cliente
        MissingColumns(result, TabBodega, ("STOCK ACTUAL", cStock));

        var read = 0;
        for (var i = h.HeaderRowIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.All(TextNorm.IsEmpty)) continue;
            read++;
            var codeNorm = TextNorm.NormalizeCode(HeaderTable.Cell(row, cCode));
            if (codeNorm is null) continue;
            var stock = TextNorm.ParseDecimal(HeaderTable.Cell(row, cStock)) ?? 0m;
            result.Stock.Add(new StockRow(codeNorm, StockLocation.Bodega, stock, null, i + 1));
        }
        result.RowsRead[TabBodega] = read;
    }

    /// <summary>"A", " b ", "A - ALTA" → "A"/"B"/"C"; cualquier otra cosa → null.</summary>
    internal static string? ParseAbc(string? cell)
    {
        var c = TextNorm.Clean(cell)?.ToUpperInvariant();
        return c is { Length: > 0 } && c[0] is 'A' or 'B' or 'C' && (c.Length == 1 || !char.IsLetter(c[1])) ? c[..1] : null;
    }

    private static void MissingColumns(SheetLoadResult result, string tab, params (string Name, int? Col)[] cols)
    {
        foreach (var (name, col) in cols)
            if (col is null)
                result.Warnings.Add(new("missing_column", $"No se encontró la columna '{name}'", tab));
    }
}
