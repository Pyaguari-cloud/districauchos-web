using System.Text.Json;
using System.Text.Json.Nodes;
using Districauchos.Core.Accounts;
using Npgsql;
using NpgsqlTypes;

namespace Districauchos.Data;

/// <summary>Datos guardados para llenar solos el formulario de pago. Dato personal: nunca se publica.</summary>
public sealed record AccountProfile(
    string? Phone = null, string? DocType = null, string? DocNumber = null, string? Address = null, string? City = null);

public sealed record Account(
    Guid Id, string Email, string Name, string PasswordHash, AccountProfile Profile, JsonArray Garage,
    int FailedLogins, DateTimeOffset? LockedUntil, DateTimeOffset CreatedAt);

/// <summary>Pedido resumido para "Mis pedidos".</summary>
public sealed record AccountOrder(long Number, string Status, DateTimeOffset CreatedAt, decimal Total, string Delivery, int Items, string Summary);

/// <summary>
/// Cuentas de clientes (opcionales: la página funciona igual sin cuenta). Sesiones y enlaces de recuperación se
/// guardan solo como SHA-256 del token. Borrar la cuenta borra sus sesiones y datos; los pedidos quedan (los
/// necesita el local para facturar) pero se desligan de la cuenta.
/// </summary>
public sealed class AccountRepository(NpgsqlDataSource db)
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);
    public static readonly TimeSpan ResetLifetime = TimeSpan.FromMinutes(30);
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockTime = TimeSpan.FromMinutes(15);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS customers (
                id             uuid PRIMARY KEY,
                email          text NOT NULL UNIQUE,
                name           text NOT NULL,
                password_hash  text NOT NULL,
                profile        jsonb NOT NULL DEFAULT '{}',
                garage         jsonb NOT NULL DEFAULT '[]',
                failed_logins  int NOT NULL DEFAULT 0,
                locked_until   timestamptz,
                privacy_at     timestamptz NOT NULL,
                created_at     timestamptz NOT NULL DEFAULT now(),
                last_login_at  timestamptz
            );
            CREATE TABLE IF NOT EXISTS customer_sessions (
                token_hash   bytea PRIMARY KEY,
                customer_id  uuid NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
                created_at   timestamptz NOT NULL DEFAULT now(),
                expires_at   timestamptz NOT NULL
            );
            CREATE INDEX IF NOT EXISTS customer_sessions_customer_idx ON customer_sessions (customer_id);
            CREATE TABLE IF NOT EXISTS customer_password_resets (
                token_hash   bytea PRIMARY KEY,
                customer_id  uuid NOT NULL REFERENCES customers (id) ON DELETE CASCADE,
                expires_at   timestamptz NOT NULL,
                used_at      timestamptz
            );
            -- "Mis pedidos": solo si el pago en línea está activo (la tabla orders la crea OrderRepository).
            DO $$ BEGIN
                IF to_regclass('orders') IS NOT NULL THEN
                    ALTER TABLE orders ADD COLUMN IF NOT EXISTS customer_id uuid REFERENCES customers (id) ON DELETE SET NULL;
                    CREATE INDEX IF NOT EXISTS orders_customer_idx ON orders (customer_id, created_at DESC) WHERE customer_id IS NOT NULL;
                END IF;
            END $$;
            DELETE FROM customer_sessions WHERE expires_at < now();
            DELETE FROM customer_password_resets WHERE expires_at < now() - interval '1 day';
            """);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Crea la cuenta. Devuelve null si el correo ya está registrado.</summary>
    public async Task<Guid?> CreateAsync(string email, string name, string passwordHash, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using var cmd = db.CreateCommand("""
            INSERT INTO customers (id, email, name, password_hash, privacy_at) VALUES ($1, $2, $3, $4, now())
            ON CONFLICT (email) DO NOTHING
            """);
        cmd.Parameters.Add(new() { Value = id });
        cmd.Parameters.Add(new() { Value = email });
        cmd.Parameters.Add(new() { Value = name });
        cmd.Parameters.Add(new() { Value = passwordHash });
        return await cmd.ExecuteNonQueryAsync(ct) == 1 ? id : null;
    }

    private const string Columns = "id, email, name, password_hash, profile, garage, failed_logins, locked_until, created_at";

    private static Account Read(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetString(3),
        JsonSerializer.Deserialize<AccountProfile>(r.GetString(4), Json) ?? new(),
        JsonNode.Parse(r.GetString(5)) as JsonArray ?? [],
        r.GetInt32(6), r.IsDBNull(7) ? null : r.GetFieldValue<DateTimeOffset>(7), r.GetFieldValue<DateTimeOffset>(8));

    public async Task<Account?> GetByEmailAsync(string email, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand($"SELECT {Columns} FROM customers WHERE email = $1");
        cmd.Parameters.Add(new() { Value = email });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Read(r) : null;
    }

    /// <summary>Cuenta de una sesión vigente (token en claro, tal como lo manda la web).</summary>
    public async Task<Account?> GetBySessionAsync(string token, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand($"""
            SELECT {string.Join(", ", Columns.Split(", ").Select(c => "c." + c))}
            FROM customer_sessions s JOIN customers c ON c.id = s.customer_id
            WHERE s.token_hash = $1 AND s.expires_at > now()
            """);
        cmd.Parameters.Add(new() { Value = AccountTokens.HashOf(token) });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Read(r) : null;
    }

    public async Task<string> CreateSessionAsync(Guid customerId, CancellationToken ct)
    {
        var token = AccountTokens.New();
        await Batch(ct,
            ("INSERT INTO customer_sessions (token_hash, customer_id, expires_at) VALUES ($1, $2, $3)",
                [AccountTokens.HashOf(token), customerId, DateTimeOffset.UtcNow + SessionLifetime]),
            ("UPDATE customers SET failed_logins = 0, locked_until = NULL, last_login_at = now() WHERE id = $1", [customerId]));
        return token;
    }

    public async Task DeleteSessionAsync(string token, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("DELETE FROM customer_sessions WHERE token_hash = $1");
        cmd.Parameters.Add(new() { Value = AccountTokens.HashOf(token) });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Suma un intento fallido; al llegar a <see cref="MaxFailedLogins"/> bloquea la cuenta <see cref="LockTime"/>.</summary>
    public async Task RecordFailedLoginAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            UPDATE customers SET
                failed_logins = CASE WHEN failed_logins + 1 >= $2 THEN 0 ELSE failed_logins + 1 END,
                locked_until  = CASE WHEN failed_logins + 1 >= $2 THEN now() + $3 ELSE locked_until END
            WHERE id = $1
            """);
        cmd.Parameters.Add(new() { Value = id });
        cmd.Parameters.Add(new() { Value = MaxFailedLogins });
        cmd.Parameters.Add(new() { Value = LockTime });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateProfileAsync(Guid id, string name, AccountProfile profile, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("UPDATE customers SET name = $2, profile = $3 WHERE id = $1");
        cmd.Parameters.Add(new() { Value = id });
        cmd.Parameters.Add(new() { Value = name });
        cmd.Parameters.Add(new() { Value = JsonSerializer.Serialize(profile, Json), NpgsqlDbType = NpgsqlDbType.Jsonb });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateGarageAsync(Guid id, JsonArray garage, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("UPDATE customers SET garage = $2 WHERE id = $1");
        cmd.Parameters.Add(new() { Value = id });
        cmd.Parameters.Add(new() { Value = garage.ToJsonString(), NpgsqlDbType = NpgsqlDbType.Jsonb });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Cambia la contraseña y cierra TODAS las sesiones (también las de otros equipos).</summary>
    public async Task SetPasswordAsync(Guid id, string passwordHash, CancellationToken ct)
    {
        await Batch(ct,
            ("UPDATE customers SET password_hash = $2, failed_logins = 0, locked_until = NULL WHERE id = $1", [id, passwordHash]),
            ("DELETE FROM customer_sessions WHERE customer_id = $1", [id]),
            ("UPDATE customer_password_resets SET used_at = now() WHERE customer_id = $1 AND used_at IS NULL", [id]));
    }

    public async Task<string> CreatePasswordResetAsync(Guid id, CancellationToken ct)
    {
        var token = AccountTokens.New();
        await Batch(ct,
            ("UPDATE customer_password_resets SET used_at = now() WHERE customer_id = $1 AND used_at IS NULL", [id]),
            ("INSERT INTO customer_password_resets (token_hash, customer_id, expires_at) VALUES ($1, $2, $3)",
                [AccountTokens.HashOf(token), id, DateTimeOffset.UtcNow + ResetLifetime]));
        return token;
    }

    /// <summary>Gasta el enlace de recuperación (una sola vez). Devuelve la cuenta o null si venció o ya se usó.</summary>
    public async Task<Guid?> ConsumePasswordResetAsync(string token, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            UPDATE customer_password_resets SET used_at = now()
            WHERE token_hash = $1 AND used_at IS NULL AND expires_at > now()
            RETURNING customer_id
            """);
        cmd.Parameters.Add(new() { Value = AccountTokens.HashOf(token) });
        return await cmd.ExecuteScalarAsync(ct) is Guid id ? id : null;
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("DELETE FROM customers WHERE id = $1");
        cmd.Parameters.Add(new() { Value = id });
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<AccountOrder>> GetOrdersAsync(Guid id, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT number, status, created_at, total, delivery, jsonb_array_length(lines),
                   coalesce((SELECT string_agg(l->>'title', ' · ') FROM (SELECT * FROM jsonb_array_elements(lines) LIMIT 3) x(l)), '')
            FROM orders WHERE customer_id = $1 AND status IN ('Paid', 'Review', 'Canceled')
            ORDER BY created_at DESC LIMIT 50
            """);
        cmd.Parameters.Add(new() { Value = id });
        var list = new List<AccountOrder>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new(r.GetInt64(0), r.GetString(1), r.GetFieldValue<DateTimeOffset>(2), r.GetDecimal(3), r.GetString(4), r.GetInt32(5), r.GetString(6)));
        return list;
    }

    /// <summary>Varias instrucciones con parámetros en una sola transacción (PostgreSQL no las acepta juntas en un comando).</summary>
    private async Task Batch(CancellationToken ct, params (string Sql, object[] Args)[] commands)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var batch = new NpgsqlBatch(conn, tx);
        foreach (var (sql, args) in commands)
        {
            var c = new NpgsqlBatchCommand(sql);
            foreach (var a in args) c.Parameters.Add(new NpgsqlParameter { Value = a });
            batch.BatchCommands.Add(c);
        }
        await batch.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task LinkOrderAsync(long number, Guid customerId, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("UPDATE orders SET customer_id = $2 WHERE number = $1");
        cmd.Parameters.Add(new() { Value = number });
        cmd.Parameters.Add(new() { Value = customerId });
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
