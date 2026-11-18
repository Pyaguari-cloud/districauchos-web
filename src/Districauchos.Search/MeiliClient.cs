using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Districauchos.Search;

/// <summary>Cliente HTTP mínimo de Meilisearch (solo lo que usa el catálogo).</summary>
public sealed class MeiliClient
{
    private readonly HttpClient _http;

    /// <summary>Solo para documentos del índice (campos en snake_case: oem_aliases, is_vch…).</summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    /// <summary>Comandos de Meilisearch (primaryKey, rankingRules…): nombres tal cual.</summary>
    private static readonly JsonSerializerOptions Commands = new() { PropertyNamingPolicy = null };

    /// <summary>Cliente de larga vida; las conexiones se renuevan cada 5 min (evita DNS viejo).</summary>
    public static MeiliClient Create(Districauchos.Core.AppSettings s) =>
        new(new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) })
        {
            Timeout = TimeSpan.FromSeconds(60),
        }, s.MeiliUrl, s.MeiliMasterKey);

    public MeiliClient(HttpClient http, string baseUrl, string? apiKey)
    {
        _http = http;
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        if (!string.IsNullOrEmpty(apiKey))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>Encola una operación y espera a que Meilisearch la termine.</summary>
    public async Task RunTaskAsync(HttpMethod method, string path, object? body, CancellationToken ct,
        TimeSpan? timeout = null, bool isDocuments = false)
    {
        using var req = new HttpRequestMessage(method, path)
        {
            Content = body is null ? null : JsonContent.Create(body, body.GetType(), options: isDocuments ? Json : Commands),
        };
        using var res = await _http.SendAsync(req, ct);
        var json = await ReadAsync(res, ct);
        var taskUid = json["taskUid"]?.GetValue<long>() ?? throw new InvalidOperationException($"Meilisearch no devolvió taskUid para {path}");
        await WaitTaskAsync(taskUid, timeout ?? TimeSpan.FromMinutes(5), ct);
    }

    public async Task WaitTaskAsync(long taskUid, TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;
        var delay = TimeSpan.FromMilliseconds(100);
        while (true)
        {
            var task = await ReadAsync(await _http.GetAsync($"tasks/{taskUid}", ct), ct);
            switch (task["status"]?.GetValue<string>())
            {
                case "succeeded": return;
                case "failed":
                case "canceled":
                    throw new InvalidOperationException($"Tarea Meilisearch {taskUid} falló: {task["error"]?.ToJsonString()}");
            }
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"Tarea Meilisearch {taskUid} no terminó a tiempo");
            await Task.Delay(delay, ct);
            delay = TimeSpan.FromMilliseconds(Math.Min(1000, delay.TotalMilliseconds * 2));
        }
    }

    public async Task<JsonNode> PostAsync(string path, object body, CancellationToken ct) =>
        await ReadAsync(await _http.PostAsJsonAsync(path, body, Commands, ct), ct);

    public async Task<bool> IndexExistsAsync(string uid, CancellationToken ct)
    {
        using var res = await _http.GetAsync($"indexes/{uid}", ct);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        try
        {
            using var res = await _http.GetAsync("health", ct);
            return res.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
    }

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage res, CancellationToken ct)
    {
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"Meilisearch {(int)res.StatusCode}: {text}", null, res.StatusCode);
        return JsonNode.Parse(text) ?? new JsonObject();
    }
}
