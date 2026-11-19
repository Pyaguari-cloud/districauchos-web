using System.Text.Json.Nodes;
using Districauchos.Core.Catalog;
using Districauchos.Core.Text;

namespace Districauchos.Search;

/// <summary>Documento del índice <c>products</c>: una ficha OEM. Solo datos públicos (sin costo ni cantidades).</summary>
public sealed record SearchDocument(
    string Id,
    string Slug,
    string Title,
    string Description,
    string Oem,
    IReadOnlyList<string> OemAliases,
    IReadOnlyList<string> Codes,
    IReadOnlyList<string> AltCodes,
    IReadOnlyList<string> Brands,
    IReadOnlyList<string> Models,
    /// <summary>"Marca|Modelo" para filtrar por combinación exacta (Chevrolet|Aveo).</summary>
    IReadOnlyList<string> Vehicles,
    string? Category,
    string? Subcategory,
    bool Available,
    int AvailableRank,
    bool IsVch,
    int VchRank,
    string? AbcClass,
    int AbcRank,
    decimal PvpMin,
    decimal PvpMax,
    decimal? MayorMin,
    string? Image);

/// <summary>Índice Meilisearch del catálogo (brief §6.6).</summary>
public sealed class ProductIndex(MeiliClient meili)
{
    public const string Uid = "products";
    private const string TmpUid = "products_tmp";

    public static IReadOnlyList<SearchDocument> BuildDocuments(CatalogBuildResult catalog)
    {
        var vehicles = catalog.Vehicles.ToLookup(v => v.OemNorm);
        return catalog.Items.Select(i =>
        {
            var v = vehicles[i.OemNorm].ToList();
            return new SearchDocument(
                Id: i.Slug,
                Slug: i.Slug,
                Title: i.Title,
                Description: i.Description,
                Oem: i.OemNorm,
                OemAliases: i.OemAliases,
                Codes: i.Codes,
                AltCodes: i.AltCodes,
                Brands: v.Select(x => x.Brand).Distinct().ToList(),
                Models: v.Where(x => x.Model != "").Select(x => x.Model).Distinct().ToList(),
                Vehicles: v.Where(x => x.Model != "").Select(x => $"{x.Brand}|{x.Model}").Distinct().ToList(),
                Category: i.Category,
                Subcategory: i.Subcategory,
                Available: i.Available,
                AvailableRank: i.Available ? 1 : 0,
                IsVch: i.IsVch,
                VchRank: i.IsVch ? 1 : 0,
                AbcClass: i.AbcClass,
                AbcRank: i.AbcClass switch { "A" => 1, "B" => 2, "C" => 3, _ => 4 },
                PvpMin: i.PvpMin,
                PvpMax: i.PvpMax,
                MayorMin: i.MayorMin,
                Image: null); // semana 2: fotos desde Drive
        }).ToList();
    }

    internal static object Settings => new
    {
        searchableAttributes = new[]
        {
            "title", "oem", "oem_aliases", "codes", "alt_codes", "models", "brands", "category", "subcategory", "description",
        },
        filterableAttributes = new[] { "brands", "models", "vehicles", "category", "subcategory", "available", "is_vch", "abc_class" },
        sortableAttributes = new[] { "pvp_min", "abc_rank", "vch_rank", "title" },
        // Relevancia primero; luego clase A > B > C; luego disponibles primero (brief §6.6).
        rankingRules = new[] { "words", "typo", "proximity", "attribute", "sort", "exactness", "vch_rank:desc", "abc_rank:asc", "available_rank:desc" },
        // Códigos y OEM deben coincidir exacto: un error de tipeo ahí devolvería otra pieza.
        typoTolerance = new { disableOnAttributes = new[] { "oem", "oem_aliases", "codes", "alt_codes" } },
        synonyms = Synonyms,
        pagination = new { maxTotalHits = 5000 },
        faceting = new { maxValuesPerFacet = 300 },
    };

    /// <summary>Sinónimos iniciales del brief; ampliar con el cliente.</summary>
    internal static Dictionary<string, string[]> Synonyms
    {
        get
        {
            var groups = new[]
            {
                new[] { "aveo", "chevytaxi" },
                new[] { "chevrolet", "chev", "gm" },
                new[] { "manguera", "mangera" },
                new[] { "buje", "bocin" },
            };
            var dict = new Dictionary<string, string[]>();
            foreach (var g in groups)
                foreach (var w in g)
                    dict[w] = g.Where(x => x != w).ToArray();
            return dict;
        }
    }

    /// <summary>
    /// Paso 1: carga todo en products_tmp. Si falla, la búsqueda pública sigue con el índice anterior.
    /// </summary>
    public async Task PrepareAsync(IReadOnlyList<SearchDocument> docs, CancellationToken ct)
    {
        if (await meili.IndexExistsAsync(TmpUid, ct))
            await meili.RunTaskAsync(HttpMethod.Delete, $"indexes/{TmpUid}", null, ct);

        await meili.RunTaskAsync(HttpMethod.Post, "indexes", new { uid = TmpUid, primaryKey = "id" }, ct);
        await meili.RunTaskAsync(HttpMethod.Patch, $"indexes/{TmpUid}/settings", Settings, ct);
        foreach (var batch in docs.Chunk(1000))
            await meili.RunTaskAsync(HttpMethod.Post, $"indexes/{TmpUid}/documents", batch, ct, isDocuments: true);
    }

    /// <summary>Paso 2: intercambio atómico products ↔ products_tmp (llamar tras guardar en Postgres).</summary>
    public async Task SwapAsync(CancellationToken ct)
    {
        if (!await meili.IndexExistsAsync(Uid, ct))
            await meili.RunTaskAsync(HttpMethod.Post, "indexes", new { uid = Uid, primaryKey = "id" }, ct);

        await meili.RunTaskAsync(HttpMethod.Post, "swap-indexes", new[] { new { indexes = new[] { Uid, TmpUid } } }, ct);
        await meili.RunTaskAsync(HttpMethod.Delete, $"indexes/{TmpUid}", null, ct);
    }

    public sealed record Query(
        string? Q, string? Brand, string? Model, string? Category, string? Subcategory,
        bool? Available, int Page = 1, int PageSize = 24, bool? IsVch = null);

    /// <summary>
    /// Exige todas las palabras ("manguera aveo" = mangueras de Aveo, no todo lo que diga "aveo").
    /// Si así no hay resultados, reintenta permitiendo que falten palabras.
    /// </summary>
    public async Task<JsonNode> SearchAsync(Query query, CancellationToken ct)
    {
        var strict = await SearchAsync(query, "all", ct);
        var multiWord = NormalizeQuery(query.Q).Contains(' ');
        if (!multiWord || (strict["totalHits"]?.GetValue<int>() ?? 0) > 0) return strict;
        var relaxed = await SearchAsync(query, "last", ct);
        relaxed["relaxed"] = true;
        return relaxed;
    }

    private Task<JsonNode> SearchAsync(Query query, string matchingStrategy, CancellationToken ct)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Brand)) filters.Add($"brands = {Quote(query.Brand)}");
        if (!string.IsNullOrWhiteSpace(query.Brand) && !string.IsNullOrWhiteSpace(query.Model))
            filters.Add($"vehicles = {Quote($"{query.Brand}|{query.Model}")}");
        if (!string.IsNullOrWhiteSpace(query.Category)) filters.Add($"category = {Quote(query.Category)}");
        if (!string.IsNullOrWhiteSpace(query.Subcategory)) filters.Add($"subcategory = {Quote(query.Subcategory)}");
        if (query.Available is { } a) filters.Add($"available = {(a ? "true" : "false")}");
        if (query.IsVch is { } v) filters.Add($"is_vch = {(v ? "true" : "false")}");

        var body = new Dictionary<string, object?>
        {
            ["q"] = NormalizeQuery(query.Q),
            ["matchingStrategy"] = matchingStrategy,
            ["filter"] = filters,
            ["page"] = Math.Max(1, query.Page),
            ["hitsPerPage"] = Math.Clamp(query.PageSize, 1, 60),
            ["facets"] = new[] { "brands", "category" },
            ["attributesToRetrieve"] = new[]
            {
                "slug", "title", "oem", "brands", "models", "category", "subcategory",
                "available", "is_vch", "abc_class", "pvp_min", "pvp_max", "mayor_min", "image",
            },
        };
        // Sin texto de búsqueda: primero importación directa (pedido del cliente), luego clase A.
        if (string.IsNullOrWhiteSpace(query.Q)) body["sort"] = new[] { "vch_rank:desc", "abc_rank:asc" };

        return meili.PostAsync($"indexes/{Uid}/search", body, ct);
    }

    /// <summary>
    /// Si la búsqueda parece un código ("96-180.071"), se normaliza igual que el OEM ("96180071")
    /// para que coincida exacto. Si es texto, se deja como está.
    /// </summary>
    internal static string NormalizeQuery(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return "";
        var t = q.Trim();
        var looksLikeCode = !t.Contains(' ') && t.Any(char.IsAsciiDigit) && t.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '/');
        return looksLikeCode ? TextNorm.NormalizeOem(t) ?? t : t;
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
}
