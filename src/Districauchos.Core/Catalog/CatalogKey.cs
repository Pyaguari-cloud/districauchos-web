namespace Districauchos.Core.Catalog;

/// <summary>
/// Clave de ficha. Con OEM es el <c>oem_norm</c> (agrupa variantes de varios proveedores).
/// Sin OEM (aprobado por el cliente 2026-11-19) cada código es su propia ficha: <c>COD:{codigo}</c>.
/// El prefijo no puede chocar con un OEM real porque <c>oem_norm</c> nunca lleva ":".
/// La web usa la misma regla en <c>web/src/lib/format.ts</c> (refLabel).
/// </summary>
public static class CatalogKey
{
    public const string CodePrefix = "COD:";

    public static string ForCode(string codeNorm) => CodePrefix + codeNorm;

    public static bool IsCode(string key) => key.StartsWith(CodePrefix, StringComparison.Ordinal);

    /// <summary>OEM real de la ficha, o null si es una ficha por código.</summary>
    public static string? Oem(string key) => IsCode(key) ? null : key;

    /// <summary>Cómo se identifica la pieza ante una persona: "96180071 (cód. X1)" o "cód. DH'40".</summary>
    public static string Label(string key, string? code, bool showCode) =>
        IsCode(key) ? $"cód. {code ?? key[CodePrefix.Length..]}"
        : showCode ? $"{key} (cód. {code})"
        : key;
}
