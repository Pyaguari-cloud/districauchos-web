using Districauchos.Core.Model;
using Districauchos.Core.Text;

namespace Districauchos.Core.Catalog;

/// <summary>
/// Fotos de productos: archivos <c>{CLAVE}.webp</c> en <c>MEDIA_DIR/productos</c> (no van a git; se suben con
/// <c>deploy.sh --fotos</c>). CLAVE = OEM en mayúsculas solo con letras y números, así <c>8'94408'840'1</c>,
/// <c>8944088401</c> y <c>89440-88401</c> comparten foto. Se asignan en cada sincronización (la tabla se reescribe),
/// y Caddy las sirve en <c>/fotos/</c>. Ficha sin OEM (<c>COD:DH'40</c>): la clave sale del código del local (<c>DH40</c>).
/// Sin OEM también sirve el código alterno (<c>C2058</c>).
/// Las fotos de la carpeta de Drive del cliente (<c>DrivePhotoSync</c>) van en otra carpeta y tienen prioridad.
/// </summary>
public static class ProductImages
{
    public const string UrlPrefix = "/fotos/";

    public static string? Key(string oem)
    {
        if (CatalogKey.IsCode(oem)) oem = oem[CatalogKey.CodePrefix.Length..];
        var key = new string(TextNorm.StripAccents(oem).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();
        return key.Length == 0 ? null : key;
    }

    /// <summary>Clave → versión (fecha del archivo) para que el navegador baje la foto nueva si se reemplaza.</summary>
    public static IReadOnlyDictionary<string, long> Scan(string dir)
    {
        if (!Directory.Exists(dir)) return new Dictionary<string, long>();
        return Directory.EnumerateFiles(dir, "*.webp")
            .ToDictionary(
                f => Path.GetFileNameWithoutExtension(f).ToUpperInvariant(),
                f => new DateTimeOffset(File.GetLastWriteTimeUtc(f)).ToUnixTimeSeconds(),
                StringComparer.Ordinal);
    }

    /// <summary>Varias carpetas: si una clave está en dos, gana la última (Drive va al final).</summary>
    public static IReadOnlyDictionary<string, long> Scan(params string[] dirs)
    {
        var all = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var dir in dirs)
            foreach (var (k, v) in Scan(dir)) all[k] = v;
        return all;
    }

    /// <summary>Pone la foto a cada ficha cuyo OEM (o alias; sin OEM, su código o código alterno) tenga archivo. Devuelve cuántas quedaron con foto.</summary>
    public static int Attach(CatalogBuildResult catalog, IReadOnlyDictionary<string, long> photos)
    {
        if (photos.Count == 0) return 0;
        var count = 0;
        for (var i = 0; i < catalog.Items.Count; i++)
        {
            var item = catalog.Items[i];
            // Con OEM, solo el OEM: el código del local a veces es el OEM de OTRO producto (MD797 tiene código HPA0001PR).
            // Sin OEM: el código del local o el código alterno (DB001 / C2058).
            var names = CatalogKey.IsCode(item.OemNorm)
                ? item.Codes.Prepend(item.OemNorm).Concat(item.AltCodes)
                : item.OemAliases.Prepend(item.OemNorm);
            var key = names.Select(Key)
                .FirstOrDefault(k => k is not null && photos.ContainsKey(k));
            if (key is null) continue;
            catalog.Items[i] = item with { Image = $"{UrlPrefix}{key}.webp?v={photos[key]}" };
            count++;
        }
        catalog.Stats.ItemsWithImage = count;
        return count;
    }

    /// <summary>
    /// Foto elegida a mano para una categoría del inicio: archivo <c>CAT-{nombre}</c> (ej. <c>CAT-Mangueras.jpg</c>,
    /// <c>CAT-Bases de motor y caja.jpg</c>; tildes, espacios y mayúsculas no importan).
    /// </summary>
    public static string CategoryKey(string category) =>
        "CAT" + new string(TextNorm.StripAccents(category).Where(char.IsAsciiLetterOrDigit).ToArray()).ToUpperInvariant();

    /// <summary>Categoría → foto elegida a mano. Las que no tengan, la API les pone la de uno de sus repuestos.</summary>
    public static int AttachCategories(CatalogBuildResult catalog, IReadOnlyDictionary<string, long> photos)
    {
        catalog.CategoryImages.Clear();
        foreach (var category in catalog.Items.Select(i => i.Category).OfType<string>().Distinct())
        {
            var key = CategoryKey(category);
            if (photos.TryGetValue(key, out var v)) catalog.CategoryImages[category] = $"{UrlPrefix}{key}.webp?v={v}";
        }
        return catalog.CategoryImages.Count;
    }
}
