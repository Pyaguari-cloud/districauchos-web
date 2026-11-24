using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Districauchos.Core;
using Districauchos.Core.Vehicles;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

namespace Districauchos.Api.Plates;

/// <summary>
/// Respuesta de la búsqueda por placa. Solo datos del vehículo: el proveedor no entrega datos del dueño en esta
/// consulta y aquí tampoco se piden ni se guardan. Brand/CatalogModel/Engine = cómo se llama en nuestro catálogo.
/// </summary>
public sealed record PlateResult(
    string Plate, bool Found, string? Make, string? Model, int? Year, string? VehicleClass, string? Vin,
    string? Brand, string? CatalogModel, string? Engine);

/// <summary>
/// Búsqueda por placa (pedido del cliente 2026-11-23, como Mansuera) con consultas.ec (prepago, 1 crédito por
/// placa). Cada placa encontrada se guarda para siempre: buscarla otra vez no gasta crédito. Límites para cuidar el
/// saldo: 12 búsquedas por hora por persona y un tope diario de consultas pagadas (PLATE_DAILY_LIMIT).
/// Solo la web (Next.js) llama a esta ruta; la IP real del visitante llega en X-Client-IP.
/// </summary>
public static class PlateEndpoints
{
    private const string Policy = "plate";
    public static bool Enabled(AppSettings s) => !string.IsNullOrWhiteSpace(s.ConsultasEcKey);

    public static void AddPlates(this WebApplicationBuilder builder, AppSettings settings)
    {
        builder.Services.AddSingleton<PlateRepository>();
        builder.Services.AddSingleton(_ => PlateVehicles.Load(settings.VehiclesJsonPath));
        builder.Services.AddHttpClient<PlateProvider>(c =>
        {
            c.BaseAddress = new Uri("https://consultas.ec/");
            c.Timeout = TimeSpan.FromSeconds(25);
            c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            c.DefaultRequestHeaders.UserAgent.ParseAdd("districauchos-web/1.0");
        });
        builder.Services.AddRateLimiter(o => o.AddPolicy(Policy, ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Request.Headers["X-Client-IP"].ToString() is { Length: > 0 and <= 64 } ip ? ip : ctx.Connection.RemoteIpAddress?.ToString() ?? "?",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 12, Window = TimeSpan.FromHours(1), QueueLimit = 0 })));
    }

    public static void MapPlates(this WebApplication app, AppSettings settings)
    {
        app.MapGet("/api/plate/status", () => Results.Ok(new { enabled = Enabled(settings) }));

        app.MapGet("/api/plate/{plate}", async (
            string plate, PlateRepository repo, PlateProvider provider, PlateVehicles vehicles, ILogger<PlateRepository> log,
            CancellationToken ct) =>
        {
            if (!Enabled(settings)) return Results.NotFound(new { error = "La búsqueda por placa no está disponible." });
            var norm = Plate.Normalize(plate);
            if (norm is null) return Results.BadRequest(new { error = "Escribe la placa así: ABC-1234 (o AB-123C si es moto)." });

            var cached = await repo.GetFreshAsync(norm, ct);
            if (cached is not null) return Results.Ok(vehicles.Describe(norm, cached));

            if (!await repo.TryReserveDailyAsync(settings.PlateDailyLimit, ct))
            {
                log.LogWarning("Placa: se alcanzó el tope diario de {Limit} consultas", settings.PlateDailyLimit);
                return Busy();
            }

            var (outcome, data) = await provider.LookupAsync(norm, settings.ConsultasEcKey!, ct);
            switch (outcome)
            {
                case ProviderOutcome.Found or ProviderOutcome.NotFound:
                    var row = data ?? PlateRow.Missing;
                    await repo.SaveAsync(norm, row, ct);
                    return Results.Ok(vehicles.Describe(norm, row));
                case ProviderOutcome.NoCredit:
                    log.LogError("Placa: el proveedor dice que no hay saldo. Recargar créditos en consultas.ec/cuenta");
                    return Busy();
                default:
                    return Results.Json(new { error = "No pudimos consultar la placa en este momento (la ANT no responde). Inténtalo en unos minutos o elige tu vehículo en la lista." },
                        statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireRateLimiting(Policy);
    }

    private static IResult Busy() => Results.Json(
        new { error = "La búsqueda por placa no está disponible por hoy. Elige tu vehículo en la lista de marca y modelo." },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}

/// <summary>Datos del vehículo tal como los entrega el proveedor (marca, modelo, año, clase, chasis).</summary>
public sealed record PlateRow(bool Found, string? Make, string? Model, int? Year, string? VehicleClass, string? Vin)
{
    public static readonly PlateRow Missing = new(false, null, null, null, null, null);
}

public enum ProviderOutcome { Found, NotFound, NoCredit, Unavailable }

/// <summary>Cliente de consultas.ec: GET /vehiculo/{placa} con X-Credits-Token. Solo se leen los campos del vehículo.</summary>
public sealed class PlateProvider(HttpClient http, ILogger<PlateProvider> log)
{
    public async Task<(ProviderOutcome, PlateRow?)> LookupAsync(string plate, string key, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"vehiculo/{plate}");
            req.Headers.Add("X-Credits-Token", key);
            using var res = await http.SendAsync(req, ct);
            if (res.Headers.TryGetValues("X-Credits-Remaining", out var left) && int.TryParse(left.FirstOrDefault(), out var n) && n <= 20)
                log.LogWarning("Placa: quedan {Credits} créditos en consultas.ec", n);

            if (res.StatusCode is HttpStatusCode.NotFound) return (ProviderOutcome.NotFound, null);
            if (res.StatusCode is HttpStatusCode.PaymentRequired or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return (ProviderOutcome.NoCredit, null);
            if (!res.IsSuccessStatusCode)
            {
                log.LogWarning("Placa: el proveedor respondió {Status}", (int)res.StatusCode);
                return (ProviderOutcome.Unavailable, null);
            }

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStreamAsync(ct));
            var root = doc.RootElement;
            string? Str(string name) =>
                root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString()!.Trim() is { Length: > 0 and <= 120 } t ? t : null;
            int? year = root.TryGetProperty("anio", out var a)
                ? a.ValueKind == JsonValueKind.Number && a.TryGetInt32(out var y) ? y
                : a.ValueKind == JsonValueKind.String && int.TryParse(a.GetString(), out var ys) ? ys : null
                : null;

            var make = Str("marca");
            if (make is null) return (ProviderOutcome.NotFound, null);
            var vin = Str("chasis") is { Length: 17 } c && c.All(char.IsAsciiLetterOrDigit) ? c.ToUpperInvariant() : null;
            var maxYear = DateTime.UtcNow.Year + 1;
            if (year is not (>= 1950) || year > maxYear) year = Plate.YearFromVin(vin, maxYear);
            return (ProviderOutcome.Found, new PlateRow(true, make, Str("modelo"), year, Str("clase"), vin));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("Placa: error al consultar el proveedor ({Error})", e.GetType().Name);
            return (ProviderOutcome.Unavailable, null);
        }
    }
}

/// <summary>Placas ya consultadas (no se vuelve a pagar) y conteo diario de consultas pagadas.</summary>
public sealed class PlateRepository(NpgsqlDataSource db)
{
    public async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            CREATE TABLE IF NOT EXISTS plate_lookups (
                plate        text PRIMARY KEY,
                found        boolean NOT NULL,
                make         text,
                model        text,
                year         int,
                vclass       text,
                vin          text,
                looked_up_at timestamptz NOT NULL DEFAULT now()
            );
            CREATE TABLE IF NOT EXISTS plate_lookup_usage (day date PRIMARY KEY, calls int NOT NULL);
            """);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Encontradas: para siempre (decisión del cliente 2026-11-23). No encontradas: 1 día (puede ser un carro recién matriculado).</summary>
    public async Task<PlateRow?> GetFreshAsync(string plate, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            SELECT found, make, model, year, vclass, vin FROM plate_lookups
            WHERE plate = $1 AND (found OR looked_up_at > now() - interval '1 day')
            """);
        cmd.Parameters.AddWithValue(plate);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
        return new PlateRow(r.GetBoolean(0), S(1), S(2), r.IsDBNull(3) ? null : r.GetInt32(3), S(4), S(5));
    }

    public async Task SaveAsync(string plate, PlateRow row, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand("""
            INSERT INTO plate_lookups (plate, found, make, model, year, vclass, vin, looked_up_at)
            VALUES ($1, $2, $3, $4, $5, $6, $7, now())
            ON CONFLICT (plate) DO UPDATE SET found = EXCLUDED.found, make = EXCLUDED.make, model = EXCLUDED.model,
                year = EXCLUDED.year, vclass = EXCLUDED.vclass, vin = EXCLUDED.vin, looked_up_at = now()
            """);
        cmd.Parameters.AddWithValue(plate);
        cmd.Parameters.AddWithValue(row.Found);
        cmd.Parameters.AddWithValue((object?)row.Make ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)row.Model ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)row.Year ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)row.VehicleClass ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)row.Vin ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Suma una consulta pagada de hoy (hora de Ecuador) si no se pasó del tope. false = tope alcanzado.</summary>
    public async Task<bool> TryReserveDailyAsync(int limit, CancellationToken ct)
    {
        if (limit <= 0) return false;
        await using var cmd = db.CreateCommand("""
            INSERT INTO plate_lookup_usage (day, calls) VALUES ((now() AT TIME ZONE 'America/Guayaquil')::date, 1)
            ON CONFLICT (day) DO UPDATE SET calls = plate_lookup_usage.calls + 1 WHERE plate_lookup_usage.calls < $1
            RETURNING calls
            """);
        cmd.Parameters.AddWithValue(limit);
        return await cmd.ExecuteScalarAsync(ct) is not null;
    }
}

/// <summary>Lleva la marca/modelo de la placa a los nombres del catálogo (data/vehicles.json).</summary>
public sealed class PlateVehicles(VehicleExtractor? extractor)
{
    public static PlateVehicles Load(string path)
    {
        // En local (dotnet run desde src/...) el archivo está unas carpetas más arriba.
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null && !File.Exists(path); dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, path))) path = Path.Combine(dir.FullName, path);
        return new(File.Exists(path) ? new VehicleExtractor(VehicleDictionary.Load(path)) : null);
    }

    public PlateResult Describe(string plate, PlateRow row)
    {
        var match = row.Found ? extractor?.MatchSingle(row.Make, row.Model) : null;
        var engine = match?.Model is not null ? EngineSize.Parse(row.Model).FirstOrDefault() : null;
        return new PlateResult(Plate.Display(plate), row.Found, row.Make, row.Model, row.Year, row.VehicleClass, row.Vin,
            match?.Brand, match?.Model, engine);
    }
}
