using Districauchos.Core.Model;
using Districauchos.Sheets;

namespace Districauchos.Core.Tests;

public class SheetLoaderTests
{
    // Filas 1–5 con botones/avisos; encabezados en la fila 6.
    private static List<IReadOnlyList<string>> WithPreamble(params string[][] rows)
    {
        var all = new List<IReadOnlyList<string>> { new[] { "BOTON" }, new[] { "" }, new[] { "AVISO" }, new[] { "" }, new[] { "" } };
        all.AddRange(rows);
        return all;
    }

    [Fact]
    public void Compat_tab_keeps_only_rows_marked_si_and_splits_brands()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "OEM", "Producto (solo referencia)", "Marca", "Modelo", "Motor", "Año desde", "Año hasta", "Usar" },
            new[] { "96180071", "Manguera Aveo", "Chevrolet", "Aveo Family", "1.5", "2009", "2019", "Sí" },
            new[] { "96180071", "Manguera Aveo", "Chevrolet", "Optra", "1.8", "2012", "2016", "" },        // pendiente
            new[] { "96180071", "Manguera Aveo", "Chevrolet", "Sail", "1.4", "2012", "2016", "No" },      // rechazada
            new[] { "1161065J03", "Base motor", "Chevrolet / Suzuki", "Grand Vitara SZ", "2.0", "2017", "2008", "si" }, // años al revés
            new[] { "8'97947'087'1", "Buje", "Chevrolet", "D-Max RT50", "3.0", "", "", "SI" },          // sin años
        };
        var res = new SheetLoadResult();
        SheetLoader.LoadCompat(rows, res);
        Assert.Equal(4, res.Compat.Count);
        Assert.Contains(res.Compat, c => c.Model == "Aveo Family" && c.YearFrom == 2009 && c.YearTo == 2019);
        Assert.Contains(res.Compat, c => c.Brand == "Suzuki" && c.YearFrom == 2008 && c.YearTo == 2017);
        Assert.Contains(res.Compat, c => c.OemNorm == "8'97947'087'1" && c.YearFrom == null);
        Assert.DoesNotContain(res.Compat, c => c.Model is "Optra" or "Sail");
    }

    [Fact]
    public void Master_reads_by_header_name_and_skips_cost()
    {
        var rows = WithPreamble(
            ["Codigo Producto", "Descripcion", "precio de COSTO", "Precio venta $", "Descr. Categoria", "Desc.Sub Categ.", "OEM", "PROVEEDOR"],
            ["C1", "BUJE AVEO", "$ 3,00", "$ 7,00", "SUSPENSION", "-", "96180071", "IMPORTADORA VCH"],
            ["", "", "", "", "", "", "", ""],
            ["", "SIN CODIGO", "", "$1", "", "", "", ""]);

        var res = new SheetLoadResult();
        SheetLoader.LoadMaster(rows, res);

        var m = Assert.Single(res.Master);
        Assert.Equal(7.00m, m.Pvp);
        Assert.Null(m.Subcategory);
        Assert.Equal("96180071", m.OemNorm);
        Assert.Equal(7, m.SourceRow);
        Assert.Contains(res.Warnings, w => w.Code == "missing_code");
    }

    [Fact]
    public void Store_tabs_work_with_different_column_order_and_duplicate_stock_columns()
    {
        // Orden tipo Maldonado (distinto) y dos columnas "Stock actual".
        var rows = WithPreamble(
            ["LOCAL", "Stock actual", "OEM (codigo de parte )", "Codigo Producto", "STOCK TABIAZO", "Stock Actual"],
            ["MAL", "5", "X", "c1", "9", "4"],
            ["MAL", "-2", "X", "C2", "0", "-2"]);

        var first = new SheetLoadResult();
        SheetLoader.LoadStore(rows, "InventarioMaldonado", StockLocation.Maldonado, first, stockOccurrence: 1);
        Assert.Equal([5m, -2m], first.Stock.Select(s => s.Stock));
        Assert.Equal("C1", first.Stock[0].CodeNorm);
        Assert.Contains(first.Warnings, w => w.Code == "stock_columns_differ" && w.Message.StartsWith("1 "));

        var second = new SheetLoadResult();
        SheetLoader.LoadStore(rows, "InventarioMaldonado", StockLocation.Maldonado, second, stockOccurrence: 2);
        Assert.Equal([4m, -2m], second.Stock.Select(s => s.Stock));
    }

    [Fact]
    public void Store_stock_column_j_is_found_even_with_suffix()
    {
        // Cusubamba real: J = "STOCK ACTUAL 1", M = "Stock Actual "
        var rows = WithPreamble(
            ["Codigo Producto", "ingresos de Stock ", "STOCK ACTUAL 1", "Stock Actual "],
            ["C1", "9", "5", "4"]);
        var res = new SheetLoadResult();
        SheetLoader.LoadStore(rows, "InventarioCusubamba", StockLocation.Cusubamba, res, stockOccurrence: 1);
        Assert.Equal(5m, Assert.Single(res.Stock).Stock);
    }

    [Fact]
    public void Abc_column_is_found_with_explanation_in_header()
    {
        var rows = WithPreamble(
            ["Codigo Producto", "Stock actual", "CLASIFICACION ABC (A= ALTA ROTACION - B = REGULAR ROTACION C= BAJA ROTACION)"],
            ["C1", "1", "a"], ["C2", "1", "C - BAJA"], ["C3", "1", "ALTA"]);
        var res = new SheetLoadResult();
        SheetLoader.LoadStore(rows, "InventarioTabiazo", StockLocation.Tabiazo, res, stockOccurrence: 1);
        Assert.Equal(["A", "C", null], res.Stock.Select(s => s.AbcClass));
    }

    [Fact]
    public void Bodega_uses_stock_actual_and_finds_header_row()
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new[] { "Código de producto", "Descripción", "Stock físico bodega", "STOCK INICIAL", "STOCK ACTUAL" },
            new[] { "C1", "X", "10", "8", "3" },
        };
        var res = new SheetLoadResult();
        SheetLoader.LoadBodega(rows, res);
        Assert.Equal(3m, Assert.Single(res.Stock).Stock);
    }

    [Fact]
    public void Csv_parser_handles_quotes_and_semicolons()
    {
        var rows = CsvSheetSource.Parse("﻿a;b;c\r\n\"x;1\";\"di \"\"q\"\"\";\n");
        Assert.Equal(["a", "b", "c"], rows[0]);
        Assert.Equal(["x;1", "di \"q\"", ""], rows[1]);
    }
}
