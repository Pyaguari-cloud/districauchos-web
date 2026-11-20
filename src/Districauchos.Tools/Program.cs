using System.Globalization;
using System.Text;
using Districauchos.Core;
using Districauchos.Sheets;
using Microsoft.Extensions.Configuration;

// Uso:
//   dotnet run --project src/Districauchos.Tools -- vehicles-report [--out out/] [--compat-file <csv>]
//   dotnet run --project src/Districauchos.Tools -- import-prices --file "<excel>.xlsx" [--apply]
//     (sin --apply solo genera el reporte; con --apply guarda en price_overrides y pide sincronizar)
// Lee las hojas (o LOCAL_CSV_DIR) sin escribir nada y genera los CSV para que el cliente revise.

var command = args.FirstOrDefault() ?? "help";
var outDir = ArgValue("--out") ?? "out";

var config = new ConfigurationBuilder()
    .AddDotEnv(FindUp(".env"))
    .AddEnvironmentVariables()
    .Build();
var settings = AppSettings.From(config);
if (!File.Exists(settings.VehiclesJsonPath) && FindUp(settings.VehiclesJsonPath) is { } found)
    settings = settings with { VehiclesJsonPath = found };
if (!File.Exists(settings.CategoriesJsonPath) && FindUp(settings.CategoriesJsonPath) is { } foundCat)
    settings = settings with { CategoriesJsonPath = foundCat };

switch (command)
{
    case "vehicles-report":
        await VehiclesReport();
        return 0;
    case "headers":
        await PrintHeaders();
        return 0;
    case "import-prices":
        return await ImportPrices();
    default:
        Console.WriteLine("Comandos: vehicles-report [--out <dir>] | headers | import-prices --file <xlsx> [--apply]");
        return command == "help" ? 0 : 1;
}

async Task VehiclesReport()
{
    Directory.CreateDirectory(outDir);
    ISheetSource source = SheetSourceFactory.Create(settings);
    // --compat-file: simula la pestaña "Compatibilidades" con un CSV antes de que el cliente la cree.
    if (ArgValue("--compat-file") is { } compatFile) source = new CompatFileSource(source, compatFile);
    var snap = await CatalogSnapshot.BuildAsync(source, settings);
    var cat = snap.Catalog;
    var stamp = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // 1) Marcas y modelos detectados + cuántas fichas tiene cada uno
    var byVehicle = cat.Vehicles
        .GroupBy(v => (v.Brand, v.Model))
        .Select(g => (g.Key.Brand, Model: g.Key.Model == "" ? "(solo marca)" : g.Key.Model, Count: g.Count()))
        .OrderBy(x => x.Brand).ThenByDescending(x => x.Count);
    WriteCsv($"vehiculos_detectados_{stamp}.csv", ["Marca", "Modelo", "Fichas (OEM)"],
        byVehicle.Select(x => new[] { x.Brand, x.Model, x.Count.ToString(CultureInfo.InvariantCulture) }));

    // 1b) Detalle: cada ficha con los vehículos detectados (para revisar falsos positivos)
    var titles = cat.Items.ToDictionary(i => i.OemNorm, i => i.Title);
    WriteCsv($"detalle_vehiculos_{stamp}.csv", ["OEM", "Descripcion", "Marca", "Modelo"],
        cat.Vehicles.OrderBy(v => v.Brand).ThenBy(v => v.Model).ThenBy(v => v.OemNorm)
            .Select(v => new[] { v.OemNorm, titles[v.OemNorm], v.Brand, v.Model == "" ? "(solo marca)" : v.Model }));

    // 1c) Años leídos de la descripción (para revisar que se lean bien)
    WriteCsv($"anios_detectados_{stamp}.csv", ["OEM", "Descripcion", "Marca", "Modelo", "Desde", "Hasta", "Origen", "Version"],
        cat.VehicleYears.OrderBy(v => v.Brand).ThenBy(v => v.Model).ThenBy(v => v.YearFrom)
            .Select(v => new[] { v.OemNorm, titles[v.OemNorm], v.Brand, v.Model,
                v.YearFrom.ToString(CultureInfo.InvariantCulture), v.YearTo.ToString(CultureInfo.InvariantCulture), v.Source, v.Version }));

    // 2) Fichas sin vehículo detectado
    WriteCsv($"sin_vehiculo_{stamp}.csv", ["OEM", "Codigo", "Descripcion"],
        cat.WithoutVehicle.OrderBy(w => w.Description).Select(w => new[] { w.OemNorm, w.Code, w.Description }));

    // 3) Modelos de varias marcas sin marca en la descripción → asignados a todas (decisión del cliente)
    WriteCsv($"modelos_multimarca_{stamp}.csv", ["Modelo", "Fichas asignadas a todas sus marcas"],
        cat.AmbiguousModels.OrderByDescending(kv => kv.Value)
            .Select(kv => new[] { kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture) }));

    // 3b) Categorías propuestas (propuesta de mejora 2): hoja vs. propuesta, para que el cliente revise
    static string SourceLabel(Districauchos.Core.Catalog.CategorySource s) => s switch
    {
        Districauchos.Core.Catalog.CategorySource.Description => "por descripción",
        Districauchos.Core.Catalog.CategorySource.Sheet => "de la hoja",
        _ => "sin regla",
    };
    WriteCsv($"categorias_propuestas_{stamp}.csv", ["OEM", "Descripcion", "Categoria en la hoja", "Categoria propuesta", "Como se asigno"],
        cat.CategoryAssignments.OrderBy(a => a.Category).ThenBy(a => a.Title)
            .Select(a => new[] { a.OemNorm, a.Title, a.SheetCategory ?? "", a.Category, SourceLabel(a.Source) }));

    // 4) Resumen
    var s = cat.Stats;
    var summary = new StringBuilder()
        .AppendLine($"Reporte de datos Districauchos · {stamp} · diccionario de vehículos v{snap.VehicleDictionaryVersion}")
        .AppendLine()
        .AppendLine("Filas leídas por pestaña:");
    foreach (var (tab, n) in snap.Sheets.RowsRead) summary.AppendLine($"  {tab}: {n}");
    summary.AppendLine()
        .AppendLine($"Productos en el maestro:          {s.MasterRows}")
        .AppendLine($"  Excluidos códigos 50/51:        {s.ExcludedGeneric}")
        .AppendLine($"  Excluidos sin descripción:      {s.ExcludedNoDescription}")
        .AppendLine($"  Excluidos con PVP ≤ 0:          {s.ExcludedNoPrice}")
        .AppendLine($"  Códigos repetidos (omitidos):   {s.DuplicateCodes}")
        .AppendLine($"  Sin OEM (ficha por código):     {s.WithoutOem}")
        .AppendLine($"  Con OEM (publicables):          {s.ProductsWithOem}")
        .AppendLine($"  Celdas OEM con varios códigos:  {s.MultiOemCells}")
        .AppendLine()
        .AppendLine($"Fichas (OEM distintos):           {s.CatalogItems}")
        .AppendLine($"  Con más de una variante:        {s.ItemsWithVariants}")
        .AppendLine($"  Disponibles:                    {s.ItemsAvailable}")
        .AppendLine($"  Importadora VCH:                {s.ItemsVch}")
        .AppendLine($"  Con vehículo detectado:         {s.ItemsWithVehicle}")
        .AppendLine($"  Sin vehículo detectado:         {cat.WithoutVehicle.Count}")
        .AppendLine($"  Con años (descripción u hoja):  {cat.VehicleYears.Select(v => v.OemNorm).Distinct().Count()}")
        .AppendLine($"  Filas de Compatibilidades usadas: {s.CompatRowsApplied}")
        .AppendLine()
        .AppendLine($"Filas de stock negativas:         {s.NegativeStockRows}")
        .AppendLine($"Filas de stock con código que no existe en el maestro: {s.StockRowsUnmatched}")
        .AppendLine()
        .AppendLine($"Categorías (reglas v{snap.CategoriesVersion}):");
    foreach (var g in cat.CategoryAssignments.GroupBy(a => a.Category).OrderByDescending(g => g.Count()))
        summary.AppendLine($"  {g.Key,-32} {g.Count(),5}   (por descripción {g.Count(a => a.Source == Districauchos.Core.Catalog.CategorySource.Description)}, de la hoja {g.Count(a => a.Source == Districauchos.Core.Catalog.CategorySource.Sheet)})");
    summary.AppendLine()
        .AppendLine("Advertencias (agrupadas):");
    foreach (var g in snap.AllWarnings.GroupBy(w => w.Code))
    {
        var w = g.First();
        var where = w.Row is null ? w.Source : $"{w.Source} fila {w.Row}";
        summary.AppendLine($"  {g.Key}: {g.Count()}  — ej.: {w.Message} [{where}]");
    }

    var summaryPath = Path.Combine(outDir, $"resumen_{stamp}.txt");
    File.WriteAllText(summaryPath, summary.ToString(), new UTF8Encoding(true));
    Console.WriteLine(summary);
    Console.WriteLine($"Archivos generados en {Path.GetFullPath(outDir)}");
}

// Muestra los encabezados de cada pestaña con su letra de columna (diagnóstico, solo lectura).
async Task PrintHeaders()
{
    var source = SheetSourceFactory.Create(settings);
    var tabs = new (string? Sheet, string Tab, string Required)[]
    {
        (settings.SheetIdPrincipal, SheetLoader.TabMaster, "Codigo Producto"),
        (settings.SheetIdPrincipal, SheetLoader.TabTabiazo, "Codigo Producto"),
        (settings.SheetIdPrincipal, SheetLoader.TabCusubamba, "Codigo Producto"),
        (settings.SheetIdMaldonado, SheetLoader.TabMaldonado, "Codigo Producto"),
        (settings.SheetIdBodega, SheetLoader.TabBodega, "Codigo de producto"),
    };
    foreach (var (sheet, tab, required) in tabs)
    {
        var rows = await source.ReadTabAsync(sheet ?? "local", tab);
        var h = HeaderTable.Locate(rows, required);
        Console.WriteLine($"== {tab} (encabezados en fila {h.HeaderRowIndex + 1})");
        for (var i = 0; i < h.RawHeaders.Count; i++)
            if (!string.IsNullOrWhiteSpace(h.RawHeaders[i]))
                Console.WriteLine($"  {ColumnLetter(i),-3} {h.RawHeaders[i]}");
    }
}

// Excel "costos y precios sugeridos" → price_overrides (respuesta del cliente 2026-11-18).
// Solo se leen las columnas "ID Producto" y "precio x mayor sugerido": el costo nunca se lee ni se guarda.
async Task<int> ImportPrices()
{
    const string Source = "excel-sugeridos";
    var file = ArgValue("--file") ?? throw new ArgumentException("Falta --file <ruta del Excel>");
    var apply = args.Contains("--apply");

    static string Key(string v) => new(Districauchos.Core.Text.TextNorm.NormalizeHeader(v).Where(char.IsAsciiLetterOrDigit).ToArray());
    // Solo la fila de encabezados se lee completa (para ubicar las columnas por nombre).
    var all = Districauchos.Tools.XlsxReader.ReadFirstSheet(file).Take(20).ToList();
    var header = all.FirstOrDefault(r => r.Cells.Values.Any(v => Key(v) == "idproducto"));
    if (header.Cells is null) { Console.Error.WriteLine("No encontré la columna 'ID Producto'"); return 1; }
    string? Col(string name) => header.Cells.FirstOrDefault(kv => Key(kv.Value).StartsWith(name)).Key;
    var idCol = Col("idproducto");
    var mayorCol = Col("precioxmayorsugerido");
    if (mayorCol is null) { Console.Error.WriteLine("No encontré la columna 'precio x mayor sugerido'"); return 1; }

    var rows = Districauchos.Tools.XlsxReader.ReadFirstSheet(file, c => c == idCol || c == mayorCol)
        .Where(r => r.Row > header.Row && r.Cells.TryGetValue(idCol!, out var id) && !string.IsNullOrWhiteSpace(id))
        .Select(r => new Districauchos.Core.Pricing.SuggestedPriceRow(r.Row, r.Cells[idCol!].Trim(), r.Cells.GetValueOrDefault(mayorCol)))
        .ToList();

    var db = Districauchos.Data.CatalogRepository.CreateDataSource(settings.DatabaseUrl ?? throw new InvalidOperationException("Falta DATABASE_URL"));
    var repo = new Districauchos.Data.CatalogRepository(db);
    var (byOem, byCode) = await repo.LoadPriceTargetsAsync(default);
    var results = Districauchos.Core.Pricing.SuggestedPrices.Match(rows, byOem, byCode);

    static string Label(Districauchos.Core.Pricing.SuggestedPriceStatus s) => s switch
    {
        Districauchos.Core.Pricing.SuggestedPriceStatus.Applied => "se aplica",
        Districauchos.Core.Pricing.SuggestedPriceStatus.NoPrice => "sin precio por mayor en el Excel",
        Districauchos.Core.Pricing.SuggestedPriceStatus.InvalidPrice => "precio inválido o fila repetida",
        Districauchos.Core.Pricing.SuggestedPriceStatus.NotFound => "no está en el catálogo publicado",
        Districauchos.Core.Pricing.SuggestedPriceStatus.NotVch => "no es Importadora VCH (no se aplica)",
        _ => "precio por mayor >= precio público (no se aplica)",
    };
    string M(decimal? v) => v is null ? "" : v.Value.ToString("0.00", CultureInfo.InvariantCulture);

    Directory.CreateDirectory(outDir);
    var name = $"precios_excel_{DateTime.Now:yyyy-MM-dd}.csv";
    WriteCsv(name, ["Fila Excel", "ID Producto", "OEM en catalogo", "Precio por mayor Excel", "Precio publico", "Por mayor 40%", "Resultado"],
        results.Select(r => new[] { r.Row.ToString(), r.Id.ReplaceLineEndings(" "), r.Oem ?? "", M(r.PrecioMayor), M(r.Pvp),
            M(r.Pvp is null ? null : Math.Round(r.Pvp.Value * (1 - settings.MayorDescuento), 2)), Label(r.Status) }));

    Console.WriteLine($"Filas leídas: {rows.Count}");
    foreach (var g in results.GroupBy(r => r.Status).OrderBy(g => g.Key))
        Console.WriteLine($"  {Label(g.Key),-50} {g.Count(),4}");
    Console.WriteLine($"Reporte: {Path.Combine(outDir, name)}");

    var toApply = results.Where(r => r.Status == Districauchos.Core.Pricing.SuggestedPriceStatus.Applied)
        .Select(r => (r.Oem!, r.PrecioMayor!.Value)).ToList();
    if (!apply)
    {
        Console.WriteLine($"Vista previa: {toApply.Count} precios listos. Repite con --apply para guardarlos.");
        return 0;
    }
    await repo.ReplacePriceOverridesAsync(Source, toApply, default);
    await Districauchos.Data.SyncSignal.RequestAsync(db, default);
    Console.WriteLine($"Guardados {toApply.Count} precios ({Source}). Pedida sincronización al Worker.");
    return 0;
}

static string ColumnLetter(int index)
{
    var s = "";
    for (var n = index + 1; n > 0; n = (n - 1) / 26) s = (char)('A' + (n - 1) % 26) + s;
    return s;
}

void WriteCsv(string name, string[] header, IEnumerable<string[]> rows)
{
    // ';' y BOM UTF-8 para que Excel en español lo abra bien con tildes.
    static string Esc(string v) => v.Contains(';') || v.Contains('"') || v.Contains('\n') ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    var sb = new StringBuilder().AppendLine(string.Join(';', header.Select(Esc)));
    foreach (var r in rows) sb.AppendLine(string.Join(';', r.Select(Esc)));
    File.WriteAllText(Path.Combine(outDir, name), sb.ToString(), new UTF8Encoding(true));
}

string? ArgValue(string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static string? FindUp(string relative)
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
    {
        var p = Path.Combine(dir.FullName, relative);
        if (File.Exists(p)) return p;
    }
    return null;
}

static class DotEnvExtensions
{
    /// <summary>Carga un .env sencillo (CLAVE=valor) si existe. Sin dependencias externas.</summary>
    public static IConfigurationBuilder AddDotEnv(this IConfigurationBuilder b, string? path)
    {
        if (path is null || !File.Exists(path)) return b;
        var values = new Dictionary<string, string?>();
        foreach (var line in File.ReadAllLines(path))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith('#')) continue;
            var eq = t.IndexOf('=');
            if (eq <= 0) continue;
            var v = t[(eq + 1)..].Trim().Trim('"');
            if (v.Length > 0) values[t[..eq].Trim()] = v;
        }
        return b.AddInMemoryCollection(values);
    }
}

/// <summary>Lee todo de la fuente real, salvo la pestaña Compatibilidades, que sale de un CSV local.</summary>
sealed class CompatFileSource(ISheetSource inner, string csvPath) : ISheetSource
{
    public async Task<IReadOnlyList<IReadOnlyList<string>>> ReadTabAsync(string spreadsheetId, string tab, CancellationToken ct = default) =>
        tab == SheetLoader.TabCompat
            ? CsvSheetSource.Parse(await File.ReadAllTextAsync(csvPath, ct))
            : await inner.ReadTabAsync(spreadsheetId, tab, ct);
}
