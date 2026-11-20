using Districauchos.Core.Text;
using Districauchos.Core.Pricing;
using System.Text.Json;
using Districauchos.Core.Catalog;
using Districauchos.Core.Model;
using Npgsql;
using NpgsqlTypes;

namespace Districauchos.Data;

/// <summary>
/// Escribe en PostgreSQL (nunca en Google). Cada snapshot reemplaza las tablas derivadas
/// dentro de una sola transacción: si algo falla, queda la versión anterior (brief §4).
/// </summary>
public sealed class CatalogRepository(NpgsqlDataSource db)
{
    public static NpgsqlDataSource CreateDataSource(string databaseUrl) =>
        NpgsqlDataSource.Create(ToConnectionString(databaseUrl));

    /// <summary>Acepta "postgres://user:pass@host:5432/db" o un connection string de Npgsql.</summary>
    public static string ToConnectionString(string databaseUrl)
    {
        if (!databaseUrl.StartsWith("postgres", StringComparison.OrdinalIgnoreCase)) return databaseUrl;
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
        }.ConnectionString;
    }

    public async Task<IReadOnlyDictionary<string, PriceOverride>> LoadPriceOverridesAsync(CancellationToken ct)
    {
        var result = new Dictionary<string, PriceOverride>(StringComparer.Ordinal);
        await using var cmd = db.CreateCommand("SELECT oem_norm, precio_mayor, precio_menor, source FROM price_overrides");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            result[r.GetString(0)] = new PriceOverride(r.GetString(0),
                r.IsDBNull(1) ? null : r.GetDecimal(1), r.IsDBNull(2) ? null : r.GetDecimal(2), r.GetString(3));
        return result;
    }

    /// <summary>Fichas publicadas y códigos → clave de ficha (OEM o COD:código), para emparejar el Excel de precios sugeridos.</summary>
    public async Task<(Dictionary<string, SuggestedPriceTarget> ByOem, Dictionary<string, string> OemByCodeKey)> LoadPriceTargetsAsync(CancellationToken ct)
    {
        var byOem = new Dictionary<string, SuggestedPriceTarget>(StringComparer.Ordinal);
        await using (var cmd = db.CreateCommand("SELECT oem_norm, is_vch, pvp_min FROM catalog_items"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                byOem[r.GetString(0)] = new(r.GetString(0), r.GetBoolean(1), r.GetDecimal(2));

        var byCode = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var cmd = db.CreateCommand($"SELECT code_norm, COALESCE(oem_norm, '{CatalogKey.CodePrefix}' || code_norm) FROM products_raw"))
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
                if (TextNorm.CodeMatchKey(r.GetString(0)) is { } key) byCode.TryAdd(key, r.GetString(1));
        return (byOem, byCode);
    }

    /// <summary>Reemplaza en una transacción los precios de un origen (p. ej. "excel-sugeridos").</summary>
    public async Task<int> ReplacePriceOverridesAsync(string source, IReadOnlyCollection<(string Oem, decimal PrecioMayor)> prices, CancellationToken ct)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var del = new NpgsqlCommand("DELETE FROM price_overrides WHERE source = $1", conn, tx))
        {
            del.Parameters.Add(new() { Value = source });
            await del.ExecuteNonQueryAsync(ct);
        }
        foreach (var (oem, mayor) in prices)
        {
            await using var ins = new NpgsqlCommand("""
                INSERT INTO price_overrides (oem_norm, precio_mayor, precio_menor, source) VALUES ($1, $2, NULL, $3)
                ON CONFLICT (oem_norm) DO UPDATE SET precio_mayor = EXCLUDED.precio_mayor, source = EXCLUDED.source
                """, conn, tx);
            ins.Parameters.Add(new() { Value = oem });
            ins.Parameters.Add(new() { Value = mayor });
            ins.Parameters.Add(new() { Value = source });
            await ins.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return prices.Count;
    }

    public async Task<long> StartRunAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("INSERT INTO sync_runs (started_at, status) VALUES (now(), 'running') RETURNING id");
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task FinishRunAsync(long id, string status, IReadOnlyDictionary<string, int> rowsRead,
        int? published, IEnumerable<SyncWarning> warnings, string? error, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            UPDATE sync_runs SET finished_at = now(), status = $2, rows_read = $3, published = $4, warnings = $5, error = $6
            WHERE id = $1
            """);
        cmd.Parameters.Add(new() { Value = id });
        cmd.Parameters.Add(new() { Value = status });
        cmd.Parameters.Add(new() { Value = JsonSerializer.Serialize(rowsRead), NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new() { Value = (object?)published ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Integer });
        cmd.Parameters.Add(new() { Value = JsonSerializer.Serialize(warnings.Take(500)), NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new() { Value = (object?)error ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Idempotente: columnas agregadas después de crear la base (cantidades, 2026-11-19).</summary>
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            ALTER TABLE catalog_items    ADD COLUMN IF NOT EXISTS stock numeric(12,2) NOT NULL DEFAULT 0;
            ALTER TABLE catalog_variants ADD COLUMN IF NOT EXISTS stock numeric(12,2) NOT NULL DEFAULT 0;
            -- Años por vehículo (2026-11-20): de la descripción y, después, de la hoja de compatibilidades.
            CREATE TABLE IF NOT EXISTS product_vehicle_years (
                oem_norm  text NOT NULL REFERENCES catalog_items (oem_norm) ON DELETE CASCADE,
                brand     text NOT NULL,
                model     text NOT NULL,
                year_from int  NOT NULL,
                year_to   int  NOT NULL,
                source    text NOT NULL,
                PRIMARY KEY (oem_norm, brand, model, year_from, year_to, source)
            );
            """);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task WriteSnapshotAsync(
        IReadOnlyList<MasterRow> master, IReadOnlyList<StockRow> stock, CatalogBuildResult catalog, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var abcByOem = catalog.Items.ToDictionary(i => i.OemNorm, i => i.AbcClass);

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        await using (var truncate = new NpgsqlCommand(
            "TRUNCATE product_vehicle_years, product_vehicles, catalog_variants, catalog_items, stock_raw, products_raw", conn, tx))
            await truncate.ExecuteNonQueryAsync(ct);

        await using (var w = await conn.BeginBinaryImportAsync(
            "COPY products_raw (code, code_norm, description, pvp, category, subcategory, oem, oem_norm, supplier, alt_code, abc_class, source_row, synced_at) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var m in master)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(m.Code, ct);
                await w.WriteAsync(m.CodeNorm, ct);
                await WriteNullable(w, m.Description, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.Pvp, NpgsqlDbType.Numeric, ct);
                await WriteNullable(w, m.Category, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.Subcategory, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.OemRaw, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.OemNorm, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.Supplier, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.AltCode, NpgsqlDbType.Text, ct);
                await WriteNullable(w, m.OemNorm is { } o ? abcByOem.GetValueOrDefault(o) : null, NpgsqlDbType.Text, ct);
                await w.WriteAsync(m.SourceRow, NpgsqlDbType.Integer, ct);
                await w.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
            }
            await w.CompleteAsync(ct);
        }

        await using (var w = await conn.BeginBinaryImportAsync(
            "COPY stock_raw (code_norm, location, stock, synced_at) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var s in stock)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(s.CodeNorm, ct);
                await w.WriteAsync(s.Location.ToString().ToLowerInvariant(), ct);
                await w.WriteAsync(s.Stock, NpgsqlDbType.Numeric, ct);
                await w.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
            }
            await w.CompleteAsync(ct);
        }

        await using (var w = await conn.BeginBinaryImportAsync(
            "COPY catalog_items (oem_norm, slug, title, description, category, subcategory, is_vch, pvp_min, pvp_max, mayor_min, available, stock, abc_class, oem_aliases, codes, alt_codes, updated_at) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var i in catalog.Items)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(i.OemNorm, ct);
                await w.WriteAsync(i.Slug, ct);
                await w.WriteAsync(i.Title, ct);
                await w.WriteAsync(i.Description, ct);
                await WriteNullable(w, i.Category, NpgsqlDbType.Text, ct);
                await WriteNullable(w, i.Subcategory, NpgsqlDbType.Text, ct);
                await w.WriteAsync(i.IsVch, ct);
                await w.WriteAsync(i.PvpMin, NpgsqlDbType.Numeric, ct);
                await w.WriteAsync(i.PvpMax, NpgsqlDbType.Numeric, ct);
                await WriteNullable(w, i.MayorMin, NpgsqlDbType.Numeric, ct);
                await w.WriteAsync(i.Available, ct);
                await w.WriteAsync(i.Stock, NpgsqlDbType.Numeric, ct);
                await WriteNullable(w, i.AbcClass, NpgsqlDbType.Text, ct);
                await w.WriteAsync(i.OemAliases.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await w.WriteAsync(i.Codes.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await w.WriteAsync(i.AltCodes.ToArray(), NpgsqlDbType.Array | NpgsqlDbType.Text, ct);
                await w.WriteAsync(now, NpgsqlDbType.TimestampTz, ct);
            }
            await w.CompleteAsync(ct);
        }

        await using (var w = await conn.BeginBinaryImportAsync(
            "COPY catalog_variants (oem_norm, code, supplier, is_vch, pvp, precio_mayor, available, stock) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var v in catalog.Variants)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(v.OemNorm, ct);
                await w.WriteAsync(v.Code, ct);
                await WriteNullable(w, v.Supplier, NpgsqlDbType.Text, ct);
                await w.WriteAsync(v.IsVch, ct);
                await w.WriteAsync(v.Pvp, NpgsqlDbType.Numeric, ct);
                await WriteNullable(w, v.PrecioMayor, NpgsqlDbType.Numeric, ct);
                await w.WriteAsync(v.Available, ct);
                await w.WriteAsync(v.Stock, NpgsqlDbType.Numeric, ct);
            }
            await w.CompleteAsync(ct);
        }

        await using (var w = await conn.BeginBinaryImportAsync(
            "COPY product_vehicles (oem_norm, brand, model) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var pv in catalog.Vehicles)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(pv.OemNorm, ct);
                await w.WriteAsync(pv.Brand, ct);
                await w.WriteAsync(pv.Model, ct);
            }
            await w.CompleteAsync(ct);
        }

        await using (var w = await conn.BeginBinaryImportAsync(
            "COPY product_vehicle_years (oem_norm, brand, model, year_from, year_to, source) FROM STDIN (FORMAT BINARY)", ct))
        {
            foreach (var y in catalog.VehicleYears)
            {
                await w.StartRowAsync(ct);
                await w.WriteAsync(y.OemNorm, ct);
                await w.WriteAsync(y.Brand, ct);
                await w.WriteAsync(y.Model, ct);
                await w.WriteAsync(y.YearFrom, ct);
                await w.WriteAsync(y.YearTo, ct);
                await w.WriteAsync(y.Source, ct);
            }
            await w.CompleteAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    private static Task WriteNullable<T>(NpgsqlBinaryImporter w, T? value, NpgsqlDbType type, CancellationToken ct) =>
        value is null ? w.WriteNullAsync(ct) : w.WriteAsync(value, type, ct);
}
