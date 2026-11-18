using System.Globalization;
using System.Text;
using Districauchos.Core;
using Districauchos.Sheets;
using Microsoft.Extensions.Configuration;

// Uso:
//   dotnet run --project src/Districauchos.Tools -- vehicles-report [--out out/]
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

switch (command)
{
    case "vehicles-report":
        await VehiclesReport();
        return 0;
    case "headers":
        await PrintHeaders();
        return 0;
    default:
        Console.WriteLine("Comandos: vehicles-report [--out <dir>] | headers");
        return command == "help" ? 0 : 1;
}

async Task VehiclesReport()
{
    Directory.CreateDirectory(outDir);
    var source = SheetSourceFactory.Create(settings);
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

    // 2) Fichas sin vehículo detectado
    WriteCsv($"sin_vehiculo_{stamp}.csv", ["OEM", "Codigo", "Descripcion"],
        cat.WithoutVehicle.OrderBy(w => w.Description).Select(w => new[] { w.OemNorm, w.Code, w.Description }));

    // 3) Modelos de varias marcas sin marca en la descripción → asignados a todas (decisión del cliente)
    WriteCsv($"modelos_multimarca_{stamp}.csv", ["Modelo", "Fichas asignadas a todas sus marcas"],
        cat.AmbiguousModels.OrderByDescending(kv => kv.Value)
            .Select(kv => new[] { kv.Key, kv.Value.ToString(CultureInfo.InvariantCulture) }));

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
        .AppendLine($"  Sin OEM (no se publican):       {s.WithoutOem}")
        .AppendLine($"  Con OEM (publicables):          {s.ProductsWithOem}")
        .AppendLine($"  Celdas OEM con varios códigos:  {s.MultiOemCells}")
        .AppendLine()
        .AppendLine($"Fichas (OEM distintos):           {s.CatalogItems}")
        .AppendLine($"  Con más de una variante:        {s.ItemsWithVariants}")
        .AppendLine($"  Disponibles:                    {s.ItemsAvailable}")
        .AppendLine($"  Importadora VCH:                {s.ItemsVch}")
        .AppendLine($"  Con vehículo detectado:         {s.ItemsWithVehicle}")
        .AppendLine($"  Sin vehículo detectado:         {cat.WithoutVehicle.Count}")
        .AppendLine()
        .AppendLine($"Filas de stock negativas:         {s.NegativeStockRows}")
        .AppendLine($"Filas de stock con código que no existe en el maestro: {s.StockRowsUnmatched}")
        .AppendLine()
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
