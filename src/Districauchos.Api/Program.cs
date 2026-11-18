using Districauchos.Core;
using Districauchos.Data;
using Npgsql;

// Semana 1: solo /api/health. El resto de endpoints (brief §8) llega en las semanas 2 y 3.
var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.From(builder.Configuration);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(_ => CatalogRepository.CreateDataSource(
    settings.DatabaseUrl ?? throw new InvalidOperationException("Falta DATABASE_URL")));

var app = builder.Build();

app.MapGet("/api/health", async (NpgsqlDataSource db, CancellationToken ct) =>
{
    await using var cmd = db.CreateCommand("""
        SELECT started_at, finished_at, status, published
        FROM sync_runs WHERE status <> 'running' ORDER BY started_at DESC LIMIT 1
        """);
    await using var r = await cmd.ExecuteReaderAsync(ct);
    object? lastSync = null;
    if (await r.ReadAsync(ct))
        lastSync = new
        {
            startedAt = r.GetFieldValue<DateTimeOffset>(0),
            finishedAt = r.IsDBNull(1) ? (DateTimeOffset?)null : r.GetFieldValue<DateTimeOffset>(1),
            status = r.GetString(2),
            published = r.IsDBNull(3) ? (int?)null : r.GetInt32(3),
        };
    return Results.Ok(new { status = "ok", lastSync });
});

app.Run();
