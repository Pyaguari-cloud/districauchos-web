using Districauchos.Core;
using Districauchos.Core.Catalog;
using Districauchos.Core.Model;
using Districauchos.Core.Pricing;
using Districauchos.Core.Vehicles;

namespace Districauchos.Sheets;

/// <summary>Lectura de hojas + construcción del catálogo. Lo usan el Worker y la herramienta de reportes.</summary>
public sealed record CatalogSnapshot(SheetLoadResult Sheets, CatalogBuildResult Catalog, string VehicleDictionaryVersion)
{
    public IEnumerable<SyncWarning> AllWarnings => Sheets.Warnings.Concat(Catalog.Warnings);

    public static async Task<CatalogSnapshot> BuildAsync(
        ISheetSource source, AppSettings settings,
        IReadOnlyDictionary<string, PriceOverride>? overrides = null, CancellationToken ct = default)
    {
        var dict = VehicleDictionary.Load(settings.VehiclesJsonPath);
        var extractor = new VehicleExtractor(dict);
        var pricing = new PricingRules(settings.MayorDescuento, settings.MayorUmbral);

        var sheets = await new SheetLoader(source, settings).LoadAsync(ct);
        var catalog = new CatalogBuilder(pricing, extractor).Build(sheets.Master, sheets.Stock, overrides);
        return new CatalogSnapshot(sheets, catalog, dict.Version);
    }
}
