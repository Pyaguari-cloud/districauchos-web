using System.Text.Json;
using System.Text.Json.Serialization;
using Districauchos.Core.Orders;
using Districauchos.Core.Pricing;
using Npgsql;
using NpgsqlTypes;

namespace Districauchos.Data;

/// <summary>
/// Pedidos pagados en línea. Contiene datos personales del comprador: solo la API los lee y
/// nunca salen en endpoints públicos del catálogo.
/// </summary>
public sealed class OrderRepository(NpgsqlDataSource db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Idempotente: crea la tabla si falta (también en bases creadas antes de los pagos).</summary>
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            CREATE SEQUENCE IF NOT EXISTS orders_number_seq START 1001;
            CREATE TABLE IF NOT EXISTS orders (
                number             bigint PRIMARY KEY DEFAULT nextval('orders_number_seq'),
                token              text NOT NULL UNIQUE,
                status             text NOT NULL,
                created_at         timestamptz NOT NULL DEFAULT now(),
                paid_at            timestamptz,
                notified_at        timestamptz,
                customer           jsonb NOT NULL,
                delivery           text NOT NULL,
                lines              jsonb NOT NULL,
                subtotal           numeric(12,2) NOT NULL,
                shipping           numeric(12,2) NOT NULL,
                total              numeric(12,2) NOT NULL,
                mayor_applied      boolean NOT NULL,
                vehicle            text,
                backorder_days     int NOT NULL,
                payment_id         bigint,
                authorization_code text,
                payment_message    text
            );
            CREATE INDEX IF NOT EXISTS orders_created_idx ON orders (created_at DESC);
            """);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> CreateAsync(
        string token, Customer customer, Delivery delivery, IReadOnlyList<OrderLine> lines,
        decimal subtotal, decimal shipping, decimal total, bool mayorApplied, string? vehicle, int backorderDays, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            INSERT INTO orders (token, status, customer, delivery, lines, subtotal, shipping, total, mayor_applied, vehicle, backorder_days)
            VALUES ($1, 'Pending', $2, $3, $4, $5, $6, $7, $8, $9, $10)
            RETURNING number
            """);
        cmd.Parameters.Add(new() { Value = token });
        cmd.Parameters.Add(new() { Value = JsonSerializer.Serialize(customer, Json), NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new() { Value = delivery.ToString() });
        cmd.Parameters.Add(new() { Value = JsonSerializer.Serialize(lines, Json), NpgsqlDbType = NpgsqlDbType.Jsonb });
        cmd.Parameters.Add(new() { Value = subtotal });
        cmd.Parameters.Add(new() { Value = shipping });
        cmd.Parameters.Add(new() { Value = total });
        cmd.Parameters.Add(new() { Value = mayorApplied });
        cmd.Parameters.Add(new() { Value = (object?)vehicle ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });
        cmd.Parameters.Add(new() { Value = backorderDays });
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<Order?> GetByTokenAsync(string token, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT number, token, status, created_at, customer, delivery, lines, subtotal, shipping, total,
                   mayor_applied, vehicle, backorder_days, payment_id, authorization_code
            FROM orders WHERE token = $1
            """);
        cmd.Parameters.Add(new() { Value = token });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new Order(
            r.GetInt64(0), r.GetString(1), Enum.Parse<OrderStatus>(r.GetString(2)), r.GetFieldValue<DateTimeOffset>(3),
            JsonSerializer.Deserialize<Customer>(r.GetString(4), Json)!, Enum.Parse<Delivery>(r.GetString(5)),
            JsonSerializer.Deserialize<List<OrderLine>>(r.GetString(6), Json)!,
            r.GetDecimal(7), r.GetDecimal(8), r.GetDecimal(9), r.GetBoolean(10),
            r.IsDBNull(11) ? null : r.GetString(11), r.GetInt32(12),
            r.IsDBNull(13) ? null : r.GetInt64(13), r.IsDBNull(14) ? null : r.GetString(14));
    }

    /// <summary>
    /// Cambia el estado solo si sigue pendiente. Devuelve true si este llamado hizo el cambio
    /// (así dos confirmaciones simultáneas no mandan dos avisos).
    /// </summary>
    public async Task<bool> TryCompleteAsync(
        string token, OrderStatus status, long? paymentId, string? authorizationCode, string? message, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            UPDATE orders SET status = $2, payment_id = $3, authorization_code = $4, payment_message = $5,
                              paid_at = CASE WHEN $2 = 'Paid' THEN now() END
            WHERE token = $1 AND status = 'Pending'
            """);
        cmd.Parameters.Add(new() { Value = token });
        cmd.Parameters.Add(new() { Value = status.ToString() });
        cmd.Parameters.Add(new() { Value = (object?)paymentId ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Bigint });
        cmd.Parameters.Add(new() { Value = (object?)authorizationCode ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });
        cmd.Parameters.Add(new() { Value = (object?)Truncate(message, 300) ?? DBNull.Value, NpgsqlDbType = NpgsqlDbType.Text });
        return await cmd.ExecuteNonQueryAsync(ct) == 1;
    }

    public async Task MarkNotifiedAsync(string token, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("UPDATE orders SET notified_at = now() WHERE token = $1");
        cmd.Parameters.Add(new() { Value = token });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string? Truncate(string? s, int max) => s is null ? null : s.Length > max ? s[..max] : s;
}
