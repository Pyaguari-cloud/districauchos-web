using Districauchos.Core.Model;
using Districauchos.Core.Pricing;
using Districauchos.Core.Text;
using Districauchos.Core.Vehicles;

namespace Districauchos.Core.Catalog;

public sealed class CatalogBuildOptions
{
    /// <summary>Códigos genéricos de venta rápida (brief §6.1).</summary>
    public HashSet<string> ExcludedCodes { get; init; } = new(StringComparer.OrdinalIgnoreCase) { "50", "51" };
}

public sealed record ProductWithoutVehicle(string OemNorm, string Code, string Description);

public sealed record CategoryAssignment(string OemNorm, string Title, string? SheetCategory, string Category, CategorySource Source, string? Rule);

public sealed class CatalogBuildStats
{
    public int MasterRows { get; set; }
    public int ExcludedGeneric { get; set; }
    public int ExcludedNoDescription { get; set; }
    public int ExcludedNoPrice { get; set; }
    public int DuplicateCodes { get; set; }
    public int WithoutOem { get; set; }
    public int MultiOemCells { get; set; }
    public int ProductsWithOem { get; set; }
    public int CatalogItems { get; set; }
    public int ItemsWithVariants { get; set; }
    public int ItemsAvailable { get; set; }
    public int ItemsVch { get; set; }
    public int ItemsWithVehicle { get; set; }
    public int StockRowsUnmatched { get; set; }
    public int NegativeStockRows { get; set; }
}

public sealed class CatalogBuildResult
{
    public List<CatalogItem> Items { get; } = [];
    public List<CatalogVariant> Variants { get; } = [];
    public List<ProductVehicle> Vehicles { get; } = [];
    public List<ProductVehicleYears> VehicleYears { get; } = [];
    public List<ProductWithoutVehicle> WithoutVehicle { get; } = [];
    public Dictionary<string, int> AmbiguousModels { get; } = new(StringComparer.Ordinal);
    public List<CategoryAssignment> CategoryAssignments { get; } = [];
    public List<SyncWarning> Warnings { get; } = [];
    public CatalogBuildStats Stats { get; } = new();
}

/// <summary>
/// Convierte las filas leídas de las hojas en el catálogo publicable (brief §6.1–6.5).
/// Es una función pura: no lee ni escribe nada fuera de memoria.
/// </summary>
public sealed class CatalogBuilder(
    PricingRules pricing, VehicleExtractor vehicles, CatalogBuildOptions? options = null,
    CategoryClassifier? categories = null, TitleCase? titles = null)
{
    private readonly CatalogBuildOptions _options = options ?? new();

    public CatalogBuildResult Build(
        IEnumerable<MasterRow> masterRows,
        IEnumerable<StockRow> stockRows,
        IReadOnlyDictionary<string, PriceOverride>? overrides = null)
    {
        overrides ??= new Dictionary<string, PriceOverride>();
        var result = new CatalogBuildResult();
        var stats = result.Stats;

        var masterList = masterRows as IReadOnlyCollection<MasterRow> ?? masterRows.ToList();
        var products = SelectPublishable(masterList, result);
        var allMasterKeys = masterList.Select(m => TextNorm.CodeMatchKey(m.CodeNorm)!).ToHashSet(StringComparer.Ordinal);
        var stockTotals = AggregateStock(stockRows, products, allMasterKeys, result, out var abcByCode);

        var usedSlugs = new HashSet<string>(StringComparer.Ordinal);
        // Primero las fichas con OEM (sus URLs no deben cambiar al sumar las de código), luego las de código.
        foreach (var group in products.GroupBy(KeyOf, StringComparer.Ordinal)
                     .OrderBy(g => CatalogKey.IsCode(g.Key)).ThenBy(g => g.Key, StringComparer.Ordinal))
        {
            var oem = group.Key;
            var rows = group.OrderBy(r => r.SourceRow).ToList();
            overrides.TryGetValue(oem, out var ovr);

            var variants = rows.Select(r =>
            {
                var isVch = PricingRules.IsVch(r.Supplier);
                var stock = Math.Max(0m, stockTotals.GetValueOrDefault(TextNorm.CodeMatchKey(r.CodeNorm)!, 0m));
                return new CatalogVariant(oem, r.Code, r.Supplier, isVch, r.Pvp!.Value,
                    pricing.PrecioMayor(r.Pvp!.Value, isVch, ovr?.PrecioMayor), stock > 0, stock);
            }).ToList();

            var principalIdx = Math.Max(0, variants.FindIndex(v => v.Available));
            var principal = rows[principalIdx];
            var title = TextNorm.RepairEncoding(principal.Description!);
            // Título legible para mostrar (propuesta 5); el slug sigue saliendo del texto original
            // para que las URLs no cambien.
            var displayTitle = titles?.Format(title) ?? title;
            var descriptions = rows.Select(r => TextNorm.RepairEncoding(r.Description!))
                .Select(d => titles?.Format(d) ?? d)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Ficha por código: la URL lleva "cod-{codigo}" para no confundirse con un OEM.
            var slugKey = CatalogKey.IsCode(oem) ? $"cod-{rows[0].CodeNorm}" : oem;
            var slug = Slug.ForItem(slugKey, title);
            for (var n = 2; !usedSlugs.Add(slug); n++) slug = $"{Slug.ForItem(slugKey, title)}-{n}";

            var sheetCategory = rows.Select(r => r.Category).FirstOrDefault(c => c is not null);
            var sheetSubcategory = rows.Select(r => r.Subcategory).FirstOrDefault(c => c is not null);
            var (category, subcategory) = (sheetCategory, sheetSubcategory);
            if (categories is not null)
            {
                var cr = categories.Classify(principal.Description, sheetCategory);
                var sheetMapped = categories.Classify(null, sheetCategory).Category;
                // La subcategoría de la hoja solo tiene sentido si la hoja ya decía la misma categoría.
                (category, subcategory) = (cr.Category, sheetMapped == cr.Category ? sheetSubcategory : null);
                result.CategoryAssignments.Add(new(oem, title, sheetCategory, cr.Category, cr.Source, cr.Rule));
            }

            var mayor = variants.Where(v => v.PrecioMayor is not null).Select(v => v.PrecioMayor!.Value).ToList();
            var abc = rows.Select(r => abcByCode.GetValueOrDefault(TextNorm.CodeMatchKey(r.CodeNorm)!))
                .Where(a => a is not null).OrderBy(a => a, StringComparer.Ordinal).FirstOrDefault();

            var item = new CatalogItem(
                OemNorm: oem,
                OemAliases: rows.SelectMany(r => r.Oems).Where(o => o != oem).Distinct().ToList(),
                Slug: slug,
                Title: displayTitle,
                Description: string.Join(" | ", descriptions),
                Category: category,
                Subcategory: subcategory,
                IsVch: variants.Any(v => v.IsVch),
                PvpMin: variants.Min(v => v.Pvp),
                PvpMax: variants.Max(v => v.Pvp),
                MayorMin: mayor.Count > 0 ? mayor.Min() : null,
                Available: variants.Any(v => v.Available),
                AbcClass: abc,
                Codes: rows.Select(r => r.Code).ToList(),
                AltCodes: rows.Select(r => r.AltCode).Where(a => a is not null).Distinct().ToList()!,
                Stock: variants.Sum(v => v.Stock));

            result.Items.Add(item);
            result.Variants.AddRange(variants);
            AddVehicles(item, rows, result);
        }

        stats.CatalogItems = result.Items.Count;
        stats.ItemsWithVariants = result.Items.Count(i => i.Codes.Count > 1);
        stats.ItemsAvailable = result.Items.Count(i => i.Available);
        stats.ItemsVch = result.Items.Count(i => i.IsVch);
        stats.ItemsWithVehicle = result.Vehicles.Select(v => v.OemNorm).Distinct().Count();
        return result;
    }

    private List<MasterRow> SelectPublishable(IEnumerable<MasterRow> masterRows, CatalogBuildResult result)
    {
        var stats = result.Stats;
        var seenCodes = new HashSet<string>(StringComparer.Ordinal);
        var products = new List<MasterRow>();

        foreach (var row in masterRows)
        {
            stats.MasterRows++;
            if (_options.ExcludedCodes.Contains(row.CodeNorm)) { stats.ExcludedGeneric++; continue; }
            if (string.IsNullOrWhiteSpace(row.Description)) { stats.ExcludedNoDescription++; continue; }
            if (row.Pvp is not > 0) { stats.ExcludedNoPrice++; continue; }
            if (!seenCodes.Add(row.CodeNorm))
            {
                stats.DuplicateCodes++;
                result.Warnings.Add(new("duplicate_code", $"Código repetido {row.Code}; se usa la primera fila", "Codigos Existentes", row.SourceRow));
                continue;
            }
            // Sin OEM: se publica como ficha propia por código (aprobado por el cliente 2026-11-19).
            if (row.OemNorm is null) stats.WithoutOem++;
            else if (row.Oems.Count > 1) stats.MultiOemCells++;
            products.Add(row);
        }

        stats.ProductsWithOem = products.Count - stats.WithoutOem;
        return products;
    }

    /// <summary>Clave de ficha: el OEM, o <c>COD:{codigo}</c> si la fila no tiene OEM.</summary>
    private static string KeyOf(MasterRow r) => r.OemNorm ?? CatalogKey.ForCode(r.CodeNorm);

    /// <summary>
    /// stock_total(codigo) = Σ max(0, stock del local). Varias filas del mismo código en un local se suman antes de
    /// recortar. Los negativos NO restan (decidido 2026-11-19): en la hoja promedian -62 por local, son datos sin
    /// cuadrar, y restarlos dejaba 512 productos con unidades como "Bajo pedido". Si el total es 0 → "Bajo pedido".
    /// </summary>
    private static Dictionary<string, decimal> AggregateStock(
        IEnumerable<StockRow> stockRows, List<MasterRow> products, HashSet<string> allMasterKeys,
        CatalogBuildResult result, out Dictionary<string, string?> abcByCode)
    {
        var known = products.Select(p => TextNorm.CodeMatchKey(p.CodeNorm)!).ToHashSet(StringComparer.Ordinal);
        var perLocation = new Dictionary<(string Key, StockLocation Loc), decimal>();
        abcByCode = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var s in stockRows)
        {
            var key = TextNorm.CodeMatchKey(s.CodeNorm);
            if (key is null) continue;
            if (s.Stock < 0) result.Stats.NegativeStockRows++;
            if (!known.Contains(key))
            {
                // Solo es "huérfana" si el código no existe en el maestro (no por ser un producto no publicable).
                if (!allMasterKeys.Contains(key)) result.Stats.StockRowsUnmatched++;
                continue;
            }

            perLocation[(key, s.Location)] = perLocation.GetValueOrDefault((key, s.Location)) + s.Stock;
            if (s.AbcClass is not null) abcByCode.TryAdd(key, s.AbcClass.ToUpperInvariant());
        }

        return SumPositive(perLocation);
    }

    /// <summary>
    /// Misma regla que el catálogo, para la revisión de stock al pagar: total por código (clave
    /// <see cref="TextNorm.CodeMatchKey"/>) = Σ max(0, stock del local).
    /// </summary>
    public static Dictionary<string, decimal> PositiveTotals(IEnumerable<StockRow> stockRows)
    {
        var perLocation = new Dictionary<(string Key, StockLocation Loc), decimal>();
        foreach (var s in stockRows)
            if (TextNorm.CodeMatchKey(s.CodeNorm) is { } key)
                perLocation[(key, s.Location)] = perLocation.GetValueOrDefault((key, s.Location)) + s.Stock;
        return SumPositive(perLocation);
    }

    private static Dictionary<string, decimal> SumPositive(Dictionary<(string Key, StockLocation Loc), decimal> perLocation)
    {
        var totals = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var ((key, _), stock) in perLocation)
            totals[key] = totals.GetValueOrDefault(key) + Math.Max(0, stock);
        return totals;
    }

    private void AddVehicles(CatalogItem item, List<MasterRow> rows, CatalogBuildResult result)
    {
        var found = new HashSet<VehicleMatch>();
        var years = new HashSet<VehicleYears>();
        var maxYear = DateTime.UtcNow.Year + 1;
        foreach (var r in rows)
        {
            years.UnionWith(vehicles.ExtractYears(r.Description, maxYear));
            var ex = vehicles.Extract(r.Description);
            found.UnionWith(ex.Matches);
            foreach (var amb in ex.AmbiguousModels)
                result.AmbiguousModels[amb] = result.AmbiguousModels.GetValueOrDefault(amb) + 1;
        }

        if (found.Count == 0)
        {
            result.WithoutVehicle.Add(new(item.OemNorm, item.Codes[0], item.Title));
            return;
        }
        foreach (var m in found.OrderBy(m => m.Brand).ThenBy(m => m.Model))
            result.Vehicles.Add(new ProductVehicle(item.OemNorm, m.Brand, m.Model ?? ""));
        foreach (var y in years.OrderBy(y => y.Brand).ThenBy(y => y.Model).ThenBy(y => y.From))
            result.VehicleYears.Add(new ProductVehicleYears(item.OemNorm, y.Brand, y.Model, y.From, y.To, "descripcion"));
    }
}
