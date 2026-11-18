using System.Text.Json;
using System.Text.Json.Nodes;
using Districauchos.Core;
using Districauchos.Data;
using Districauchos.Search;

// API pública del catálogo (brief §8). Solo lectura; la cotización llega en la semana 3.
var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.From(builder.Configuration);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(_ => CatalogRepository.CreateDataSource(
    settings.DatabaseUrl ?? throw new InvalidOperationException("Falta DATABASE_URL")));
builder.Services.AddSingleton<CatalogQueries>();
builder.Services.AddSingleton(_ => MeiliClient.Create(settings));
builder.Services.AddSingleton<ProductIndex>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

var app = builder.Build();

// Caché corta en Cloudflare/navegador: los datos cambian como mucho cada 60 min.
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        if (ctx.Request.Method == "GET" && ctx.Response.StatusCode == 200 && ctx.Request.Path.StartsWithSegments("/api/catalog"))
            ctx.Response.Headers.CacheControl = "public, max-age=60, stale-while-revalidate=300";
        return Task.CompletedTask;
    });
    await next();
});

app.MapGet("/api/catalog", async (
    ProductIndex index, CancellationToken ct,
    string? q, string? brand, string? model, string? category, string? subcategory,
    bool? available, int? page, int? pageSize) =>
{
    var res = await index.SearchAsync(new ProductIndex.Query(
        q, brand, model, category, subcategory, available, page ?? 1, pageSize ?? 24), ct);

    return Results.Ok(new
    {
        items = res["hits"]?.AsArray().Select(ToCamel).ToList(),
        total = res["totalHits"]?.GetValue<int>() ?? 0,
        page = res["page"]?.GetValue<int>() ?? 1,
        totalPages = res["totalPages"]?.GetValue<int>() ?? 0,
        // true = no hubo coincidencia con todas las palabras; la web puede decir "resultados parecidos".
        relaxed = res["relaxed"]?.GetValue<bool>() ?? false,
        facets = new
        {
            brands = res["facetDistribution"]?["brands"],
            categories = res["facetDistribution"]?["category"],
        },
    });
});

// Debe ir antes de /{slug} para que "slugs" no se interprete como una ficha.
app.MapGet("/api/catalog/slugs", async (CatalogQueries queries, CancellationToken ct) =>
    Results.Ok(await queries.GetSlugsAsync(ct)));

app.MapGet("/api/catalog/{slug}", async (string slug, CatalogQueries queries, CancellationToken ct) =>
    await queries.GetBySlugAsync(slug, ct) is { } item ? Results.Ok(item) : Results.NotFound());

app.MapGet("/api/vehicles", async (CatalogQueries queries, CancellationToken ct) =>
    Results.Ok(await queries.GetVehiclesAsync(ct)));

app.MapGet("/api/categories", async (CatalogQueries queries, CancellationToken ct) =>
    Results.Ok(await queries.GetCategoriesAsync(ct)));

app.MapGet("/api/health", async (CatalogQueries queries, MeiliClient meili, CancellationToken ct) =>
{
    var lastSync = await queries.GetLastSyncAsync(ct);
    var search = await meili.IsHealthyAsync(ct);
    var ok = search && lastSync is not null;
    return Results.Json(new { status = ok ? "ok" : "degraded", search, lastSync }, statusCode: ok ? 200 : 503);
});

app.Run();

// Los documentos de Meilisearch vienen en snake_case; la API responde en camelCase.
static JsonNode? ToCamel(JsonNode? node)
{
    if (node is not JsonObject obj) return node?.DeepClone();
    var result = new JsonObject();
    foreach (var (key, value) in obj)
    {
        var parts = key.Split('_');
        var camel = parts[0] + string.Concat(parts.Skip(1).Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
        result[camel] = value?.DeepClone();
    }
    return result;
}
