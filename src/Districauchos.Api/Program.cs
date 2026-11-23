using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Districauchos.Api.Accounts;
using Districauchos.Api.Payments;
using Districauchos.Core;
using Districauchos.Core.Pricing;
using Districauchos.Data;
using Districauchos.Search;

// API del catálogo (brief §8): lectura del catálogo, cotizaciones y pedidos pagados con PayPhone.
var builder = WebApplication.CreateBuilder(args);
var settings = AppSettings.From(builder.Configuration);
builder.Services.AddSingleton(settings);
builder.Services.AddSingleton(_ => CatalogRepository.CreateDataSource(
    settings.DatabaseUrl ?? throw new InvalidOperationException("Falta DATABASE_URL")));
builder.Services.AddSingleton<CatalogQueries>();
builder.Services.AddSingleton(_ => MeiliClient.Create(settings));
builder.Services.AddSingleton<ProductIndex>();
builder.Services.AddSingleton(new QuoteCalculator(
    new PricingRules(settings.MayorDescuento, settings.MayorUmbral), nationalShipping: settings.EnvioNacional,
    galapagosShipping: settings.EnvioGalapagos));
builder.AddPayments(settings);
builder.AddAccounts();
builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();
// La API lee columnas nuevas (stock); se aseguran aquí también por si arranca antes que el Worker.
await new CatalogRepository(app.Services.GetRequiredService<Npgsql.NpgsqlDataSource>()).EnsureSchemaAsync(CancellationToken.None);
if (OrderEndpoints.Enabled(settings))
{
    await app.Services.GetRequiredService<OrderRepository>().EnsureSchemaAsync(CancellationToken.None);
    await app.Services.GetRequiredService<StockCheckRepository>().EnsureSchemaAsync(CancellationToken.None);
}
// Cuentas de clientes (después de orders: liga cada pedido a su cuenta si la hay).
await app.Services.GetRequiredService<AccountRepository>().EnsureSchemaAsync(CancellationToken.None);
app.UseRateLimiter();

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
    bool? available, bool? vch, int? page, int? pageSize, int? year, string? engine) =>
{
    var res = await index.SearchAsync(new ProductIndex.Query(
        q, brand, model, category, subcategory, available, page ?? 1, pageSize ?? 24, vch,
        year is >= 1960 and <= 2100 ? year : null,
        engine is { Length: 3 } && char.IsAsciiDigit(engine[0]) && engine[1] == '.' && char.IsAsciiDigit(engine[2]) ? engine : null), ct);

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

// Cotización del carrito (brief §6.7): los precios los pone siempre el servidor.
app.MapPost("/api/quote/preview", async (QuoteRequest req, QuoteCalculator calc, CatalogQueries queries, CancellationToken ct) =>
{
    var lines = (req.Items ?? [])
        .Where(i => !string.IsNullOrWhiteSpace(i.Oem) && i.Oem.Length <= 64 && (i.Code?.Length ?? 0) <= 64)
        .Take(QuoteCalculator.MaxLines)
        .Select(i => new QuoteRequestLine(i.Oem!.Trim(), i.Code?.Trim(), i.Qty))
        .ToList();
    if (lines.Count == 0) return Results.BadRequest(new { error = "El carrito está vacío" });

    var variants = await queries.GetQuoteVariantsAsync(lines.Select(l => l.Oem).Distinct().ToList(), ct);
    var vehicle = req.Vehicle is { Length: > 0 and <= 80 } v ? v : null;
    var delivery = Enum.TryParse<Delivery>(req.Delivery, ignoreCase: true, out var d) && Enum.IsDefined(d) ? d : Delivery.None;
    return Results.Ok(calc.Calculate(lines, variants, vehicle, delivery));
});

// Forzar sincronización (brief §8). Protegido por ADMIN_TOKEN; el Worker recibe el aviso por PostgreSQL.
var lastAdminSync = DateTimeOffset.MinValue;
app.MapPost("/api/admin/sync", async (HttpRequest http, Npgsql.NpgsqlDataSource db, CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(settings.AdminToken)) return Results.NotFound();
    var header = http.Headers.Authorization.ToString();
    var given = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : "";
    if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(settings.AdminToken)))
        return Results.Unauthorized();

    // Una hoja grande tarda ~10 s; más de un pedido por minuto no tiene sentido.
    var now = DateTimeOffset.UtcNow;
    if (now - lastAdminSync < TimeSpan.FromSeconds(60))
        return Results.Json(new { status = "espera", retryAfterSeconds = 60 - (int)(now - lastAdminSync).TotalSeconds }, statusCode: 429);
    lastAdminSync = now;

    await SyncSignal.RequestAsync(db, ct);
    return Results.Accepted(value: new { status = "pedida", hint = "Revisa /api/health en unos segundos" });
});

app.MapOrders(settings);
app.MapAccounts(settings);

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

record QuoteRequest(List<QuoteRequestItem>? Items, string? Vehicle, string? Delivery);
record QuoteRequestItem(string? Oem, string? Code, int Qty);
