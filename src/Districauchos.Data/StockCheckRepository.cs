using Districauchos.Core.Text;
using Npgsql;
using NpgsqlTypes;

namespace Districauchos.Data;

/// <summary>Ficha cuyo stock cambió en una revisión al pagar (para actualizar también el buscador).</summary>
public sealed record StockChange(string Slug, decimal Stock, bool Available);

/// <summary>
/// Revisión de stock al pagar. La API (sin acceso a Google) deja un pedido en <c>stock_checks</c> y avisa por
/// PostgreSQL; el Worker lee solo las pestañas de stock, actualiza esos códigos y marca el pedido como hecho.
/// La API espera unos segundos; si no llega a tiempo, se sigue con la última sincronización.
/// </summary>
public sealed class StockCheckRepository(NpgsqlDataSource db)
{
    public const string Channel = "districauchos_stock";

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS stock_checks (
                id           bigserial PRIMARY KEY,
                codes        text[] NOT NULL,
                requested_at timestamptz NOT NULL DEFAULT now(),
                done_at      timestamptz,
                ok           boolean,
                error        text
            );
            CREATE INDEX IF NOT EXISTS stock_checks_pending_idx ON stock_checks (requested_at) WHERE done_at IS NULL;
            """);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>API: pide la revisión y espera hasta <paramref name="timeout"/>. true = se revisó con datos frescos.</summary>
    public async Task<bool> RequestAndWaitAsync(IReadOnlyCollection<string> codes, TimeSpan timeout, CancellationToken ct)
    {
        long id;
        await using (var cmd = db.CreateCommand("INSERT INTO stock_checks (codes) VALUES ($1) RETURNING id"))
        {
            cmd.Parameters.Add(new() { Value = codes.Distinct().ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
            id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        }
        await using (var notify = db.CreateCommand($"NOTIFY {Channel}"))
            await notify.ExecuteNonQueryAsync(ct);

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(250, ct);
            await using var poll = db.CreateCommand("SELECT ok FROM stock_checks WHERE id = $1 AND done_at IS NOT NULL");
            poll.Parameters.Add(new() { Value = id });
            if (await poll.ExecuteScalarAsync(ct) is bool ok) return ok;
        }
        return false;
    }

    /// <summary>Worker: pedidos pendientes recientes (los viejos ya no tienen a nadie esperando).</summary>
    public async Task<(long[] Ids, string[] Codes)> TakePendingAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT id, codes FROM stock_checks
            WHERE done_at IS NULL AND requested_at > now() - interval '2 minutes'
            ORDER BY id
            """);
        var ids = new List<long>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            ids.Add(r.GetInt64(0));
            codes.UnionWith(r.GetFieldValue<string[]>(1));
        }
        return (ids.ToArray(), codes.ToArray());
    }

    /// <summary>
    /// Worker: aplica el stock leído a esos códigos (variantes y su ficha) y devuelve las fichas que cambiaron.
    /// <paramref name="totals"/> viene de <c>CatalogBuilder.PositiveTotals</c> (clave = CodeMatchKey).
    /// </summary>
    public async Task<IReadOnlyList<StockChange>> ApplyAsync(
        IReadOnlyCollection<string> codes, IReadOnlyDictionary<string, decimal> totals, CancellationToken ct)
    {
        var changes = new List<StockChange>();
        if (codes.Count == 0) return changes;

        var stocks = codes.Select(c =>
            TextNorm.CodeMatchKey(TextNorm.NormalizeCode(c)) is { } key ? totals.GetValueOrDefault(key) : 0m).ToArray();

        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand("""
            WITH fresh AS (SELECT * FROM unnest($1::text[], $2::numeric[]) AS f(code, stock)),
            upd AS (
                UPDATE catalog_variants v SET stock = f.stock, available = f.stock > 0
                FROM fresh f WHERE v.code = f.code AND v.stock IS DISTINCT FROM f.stock
                RETURNING v.oem_norm
            ),
            totals AS (
                SELECT v.oem_norm, sum(v.stock) AS stock, bool_or(v.available) AS available
                FROM catalog_variants v WHERE v.oem_norm IN (SELECT oem_norm FROM upd) GROUP BY v.oem_norm
            )
            UPDATE catalog_items i SET stock = t.stock, available = t.available
            FROM totals t WHERE i.oem_norm = t.oem_norm
            RETURNING i.slug, i.stock, i.available
            """, conn, tx))
        {
            cmd.Parameters.Add(new() { Value = codes.ToArray(), NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Text });
            cmd.Parameters.Add(new() { Value = stocks, NpgsqlDbType = NpgsqlDbType.Array | NpgsqlDbType.Numeric });
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) changes.Add(new(r.GetString(0), r.GetDecimal(1), r.GetBoolean(2)));
        }
        await tx.CommitAsync(ct);
        return changes;
    }

    public async Task CompleteAsync(long[] ids, bool ok, string? error, CancellationToken ct)
    {
        if (ids.Length == 0) return;
        await using var cmd = db.CreateCommand("""
            UPDATE stock_checks SET done_at = now(), ok = $2, error = $3 WHERE id = ANY($1);
            DELETE FROM stock_checks WHERE requested_at < now() - interval '7 days';
            """);
        cmd.Parameters.Add(new() { Value = ids });
        cmd.Parameters.Add(new() { Value = ok });
        cmd.Parameters.Add(new() { Value = (object?)error ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
