namespace Districauchos.Core.Model;

public enum StockLocation { Tabiazo, Cusubamba, Maldonado, Bodega }

/// <summary>
/// Fila de la pestaña "Codigos Existentes" ya limpiada celda por celda.
/// Las reglas de exclusión (sin descripción, PVP ≤ 0, códigos 50/51) las aplica CatalogBuilder.
/// No incluye el costo a propósito.
/// </summary>
public sealed record MasterRow(
    string Code,
    string CodeNorm,
    string? Description,
    decimal? Pvp,
    string? Category,
    string? Subcategory,
    string? OemRaw,
    IReadOnlyList<string> Oems,
    string? Supplier,
    string? AltCode,
    int SourceRow)
{
    /// <summary>OEM principal (el primero de la celda).</summary>
    public string? OemNorm => Oems.Count > 0 ? Oems[0] : null;
}

/// <summary>Fila de stock de un local o de la bodega. Dato interno: nunca se expone.</summary>
public sealed record StockRow(string CodeNorm, StockLocation Location, decimal Stock, string? AbcClass, int SourceRow);

public sealed record PriceOverride(string OemNorm, decimal? PrecioMayor, decimal? PrecioMenor, string Source);

public sealed record CatalogVariant(
    string OemNorm,
    string Code,
    string? Supplier,
    bool IsVch,
    decimal Pvp,
    decimal? PrecioMayor,
    bool Available);

public sealed record CatalogItem(
    string OemNorm,
    IReadOnlyList<string> OemAliases,
    string Slug,
    string Title,
    string Description,
    string? Category,
    string? Subcategory,
    bool IsVch,
    decimal PvpMin,
    decimal PvpMax,
    decimal? MayorMin,
    bool Available,
    string? AbcClass,
    IReadOnlyList<string> Codes,
    IReadOnlyList<string> AltCodes);

public sealed record ProductVehicle(string OemNorm, string Brand, string Model);

public sealed record SyncWarning(string Code, string Message, string? Source = null, int? Row = null);
