using Districauchos.Core.Text;

namespace Districauchos.Core.Pricing;

/// <summary>Reglas de precio del brief §6.4.</summary>
public sealed class PricingRules(decimal mayorDescuento = 0.40m, decimal mayorUmbral = 200m)
{
    public decimal MayorDescuento { get; } = mayorDescuento;
    public decimal MayorUmbral { get; } = mayorUmbral;

    /// <summary>True si el proveedor es Importadora VCH (tolera espacios dobles y mayúsculas).</summary>
    public static bool IsVch(string? supplier) =>
        supplier is not null && TextNorm.NormalizeForMatch(supplier).Contains("IMPORTADORA VCH");

    /// <summary>precio_mayor = round(PVP × (1 − descuento), 2), salvo override de price_overrides.</summary>
    public decimal? PrecioMayor(decimal pvp, bool isVch, decimal? overrideMayor = null)
    {
        if (!isVch) return null;
        if (overrideMayor is > 0) return overrideMayor;
        return Math.Round(pvp * (1 - MayorDescuento), 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// ¿Aplica el precio al por mayor? Confirmado por el cliente (2026-11-18): se cuentan SOLO los productos
    /// con precio por mayor (Importadora VCH), sumados YA a precio por mayor, y ese total debe ser ≥ umbral.
    /// Los demás productos van siempre a precio público y no suman.
    /// </summary>
    public bool AppliesMayor(decimal subtotalMayor) => subtotalMayor >= MayorUmbral;

    public decimal FaltaParaMayor(decimal subtotalMayor) => Math.Max(0, MayorUmbral - subtotalMayor);
}
