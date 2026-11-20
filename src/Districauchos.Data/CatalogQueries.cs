using Districauchos.Core.Pricing;
using Npgsql;

namespace Districauchos.Data;

// Respuestas públicas: nunca incluyen costo, proveedor ni ubicaciones. La cantidad sí (decisión del cliente
// 2026-11-19), pero solo el total de todos los locales.
public sealed record VariantDto(string Code, bool IsVch, decimal Pvp, decimal? PrecioMayor, bool Available, decimal Stock);
public sealed record VehicleDto(string Brand, string? Model);
public sealed record ItemDto(
    string Slug, string Oem, IReadOnlyList<string> OemAliases, string Title, string Description,
    string? Category, string? Subcategory, bool IsVch, decimal PvpMin, decimal PvpMax, decimal? MayorMin,
    bool Available, string? AbcClass, string? Image, DateTimeOffset UpdatedAt,
    IReadOnlyList<VariantDto> Variants, IReadOnlyList<VehicleDto> Vehicles, decimal Stock);
public sealed record ModelCount(string Name, int Count);
public sealed record BrandDto(string Brand, int Count, IReadOnlyList<ModelCount> Models);
public sealed record SubcategoryCount(string Name, int Count);
public sealed record CategoryDto(string Name, int Count, IReadOnlyList<SubcategoryCount> Subcategories);
public sealed record SlugDto(string Slug, DateTimeOffset UpdatedAt);
public sealed record SyncStatusDto(DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, int? Published);

/// <summary>Consultas de solo lectura para la API pública.</summary>
public sealed class CatalogQueries(NpgsqlDataSource db)
{
    public async Task<ItemDto?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        string oem;
        ItemDto item;

        await using (var cmd = new NpgsqlCommand("""
            SELECT oem_norm, oem_aliases, title, description, category, subcategory, is_vch,
                   pvp_min, pvp_max, mayor_min, available, abc_class, image_path, updated_at, stock
            FROM catalog_items WHERE slug = $1
            """, conn))
        {
            cmd.Parameters.Add(new() { Value = slug });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return null;
            oem = r.GetString(0);
            item = new ItemDto(slug, oem, r.GetFieldValue<string[]>(1), r.GetString(2), r.GetString(3),
                Str(r, 4), Str(r, 5), r.GetBoolean(6), r.GetDecimal(7), r.GetDecimal(8),
                r.IsDBNull(9) ? null : r.GetDecimal(9), r.GetBoolean(10), Str(r, 11), Str(r, 12),
                r.GetFieldValue<DateTimeOffset>(13), [], [], r.GetDecimal(14));
        }

        var variants = new List<VariantDto>();
        await using (var cmd = new NpgsqlCommand("""
            SELECT code, is_vch, pvp, precio_mayor, available, stock FROM catalog_variants
            WHERE oem_norm = $1 ORDER BY available DESC, pvp, code
            """, conn))
        {
            cmd.Parameters.Add(new() { Value = oem });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                variants.Add(new(r.GetString(0), r.GetBoolean(1), r.GetDecimal(2), r.IsDBNull(3) ? null : r.GetDecimal(3), r.GetBoolean(4), r.GetDecimal(5)));
        }

        var vehicles = new List<VehicleDto>();
        await using (var cmd = new NpgsqlCommand(
            "SELECT brand, model FROM product_vehicles WHERE oem_norm = $1 ORDER BY brand, model", conn))
        {
            cmd.Parameters.Add(new() { Value = oem });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                vehicles.Add(new(r.GetString(0), r.GetString(1) is { Length: > 0 } m ? m : null));
        }

        return item with { Variants = variants, Vehicles = vehicles };
    }

    /// <summary>Marcas y modelos para el selector de vehículo (cuántas fichas tiene cada uno).</summary>
    public async Task<IReadOnlyList<BrandDto>> GetVehiclesAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT brand, model, count(DISTINCT oem_norm)
            FROM product_vehicles GROUP BY brand, model ORDER BY brand, model
            """);
        var rows = new List<(string Brand, string Model, int Count)>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                rows.Add((r.GetString(0), r.GetString(1), (int)r.GetInt64(2)));

        // Total por marca con DISTINCT real: una ficha puede tener varios modelos de la misma marca.
        var brandTotals = new Dictionary<string, int>();
        await using (var cmd2 = db.CreateCommand("SELECT brand, count(DISTINCT oem_norm) FROM product_vehicles GROUP BY brand"))
        await using (var r = await cmd2.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                brandTotals[r.GetString(0)] = (int)r.GetInt64(1);

        return rows.GroupBy(x => x.Brand)
            .Select(g => new BrandDto(g.Key, brandTotals.GetValueOrDefault(g.Key),
                g.Where(x => x.Model != "").Select(x => new ModelCount(x.Model, x.Count)).ToList()))
            .OrderByDescending(b => b.Count)
            .ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT category, coalesce(subcategory, ''), count(*) FROM catalog_items
            WHERE category IS NOT NULL GROUP BY 1, 2 ORDER BY 1, 2
            """);
        var rows = new List<(string Cat, string Sub, int Count)>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                rows.Add((r.GetString(0), r.GetString(1), (int)r.GetInt64(2)));

        return rows.GroupBy(x => x.Cat)
            .Select(g => new CategoryDto(g.Key, g.Sum(x => x.Count),
                g.Where(x => x.Sub != "").Select(x => new SubcategoryCount(x.Sub, x.Count)).ToList()))
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    /// <summary>Todas las fichas para el sitemap.xml.</summary>
    public async Task<IReadOnlyList<SlugDto>> GetSlugsAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("SELECT slug, updated_at FROM catalog_items ORDER BY slug");
        var list = new List<SlugDto>(4000);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new(r.GetString(0), r.GetFieldValue<DateTimeOffset>(1)));
        return list;
    }

    /// <summary>Variantes publicadas para cotizar (la principal primero, igual que la ficha).</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<QuoteVariant>>> GetQuoteVariantsAsync(
        IReadOnlyCollection<string> oems, CancellationToken ct)
    {
        var result = new Dictionary<string, IReadOnlyList<QuoteVariant>>(StringComparer.Ordinal);
        if (oems.Count == 0) return result;
        await using var cmd = db.CreateCommand("""
            SELECT v.oem_norm, i.title, v.code, v.pvp, v.precio_mayor, v.available,
                   count(*) OVER (PARTITION BY v.oem_norm), v.stock
            FROM catalog_variants v JOIN catalog_items i USING (oem_norm)
            WHERE v.oem_norm = ANY($1)
            ORDER BY v.oem_norm, v.available DESC, v.pvp, v.code
            """);
        cmd.Parameters.Add(new() { Value = oems.ToArray() });
        var rows = new List<QuoteVariant>();
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                rows.Add(new(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDecimal(3),
                    r.IsDBNull(4) ? null : r.GetDecimal(4), r.GetBoolean(5), (int)r.GetInt64(6), r.GetDecimal(7)));
        foreach (var g in rows.GroupBy(x => x.Oem)) result[g.Key] = g.ToList();
        return result;
    }

    public async Task<SyncStatusDto?> GetLastSyncAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT started_at, finished_at, status, published
            FROM sync_runs WHERE status <> 'running' ORDER BY started_at DESC LIMIT 1
            """);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new(r.GetFieldValue<DateTimeOffset>(0), r.IsDBNull(1) ? null : r.GetFieldValue<DateTimeOffset>(1),
            r.GetString(2), r.IsDBNull(3) ? null : r.GetInt32(3));
    }

    private static string? Str(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
}
