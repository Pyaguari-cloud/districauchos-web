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
    bool Available,
    /// <summary>Unidades a mostrar: suma de los locales y bodega con stock positivo (los negativos no restan).</summary>
    decimal Stock = 0);

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
    IReadOnlyList<string> AltCodes,
    /// <summary>Suma de las unidades de sus variantes (nunca por local: las ubicaciones no se publican).</summary>
    decimal Stock = 0,
    /// <summary>URL de la foto (/fotos/CLAVE.webp?v=…) o null; la pone <see cref="Catalog.ProductImages"/>.</summary>
    string? Image = null);

public sealed record ProductVehicle(string OemNorm, string Brand, string Model);

/// <summary>Años de un vehículo para un repuesto. Source: "descripcion" (inventario) u "hoja" (compatibilidades verificadas).</summary>
public sealed record ProductVehicleYears(
    string OemNorm, string Brand, string Model, int YearFrom, int YearTo, string Source,
    /// <summary>Versión como la escribió la hoja ("Aveo Family", "D-Max RT50"); vacío si viene de la descripción.</summary>
    string Version = "", string Engine = "");

/// <summary>Cilindraje para un vehículo ("1.4"). Source: "descripcion" u "hoja". Sirve para el filtro "Cilindraje".</summary>
public sealed record ProductVehicleEngine(string OemNorm, string Brand, string Model, string Engine, string Source);

/// <summary>Fila de la pestaña "Compatibilidades" del cliente marcada Usar = Sí.</summary>
public sealed record CompatSheetRow(string OemNorm, string Brand, string Model, string Engine, int? YearFrom, int? YearTo, int SourceRow);

public sealed record SyncWarning(string Code, string Message, string? Source = null, int? Row = null);
