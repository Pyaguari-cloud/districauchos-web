using System.Text.Json;
using Districauchos.Core.Catalog;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;

namespace Districauchos.Sheets;

/// <summary>Archivo de la carpeta de fotos de Drive (solo lo que hace falta para saber si cambió).</summary>
public sealed record DrivePhotoFile(string Id, string Name, string? Md5, DateTimeOffset? Modified, long? Size);

public interface IDrivePhotoSource
{
    Task<IReadOnlyList<DrivePhotoFile>> ListAsync(string folderId, CancellationToken ct);
    Task<byte[]> DownloadAsync(string fileId, CancellationToken ct);
}

/// <summary>Lectura de Drive con scope <c>drive.readonly</c>: solo lista y descarga, nunca escribe ni borra en Drive.</summary>
public sealed class GoogleDrivePhotoSource : IDrivePhotoSource, IDisposable
{
    private readonly DriveService _service;

    public GoogleDrivePhotoSource(string serviceAccountJsonPath)
    {
        var credential = CredentialFactory
            .FromFile<ServiceAccountCredential>(serviceAccountJsonPath)
            .ToGoogleCredential()
            .CreateScoped(DriveService.Scope.DriveReadonly);
        _service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "districauchos-catalogo-sync",
        });
    }

    public async Task<IReadOnlyList<DrivePhotoFile>> ListAsync(string folderId, CancellationToken ct)
    {
        var files = new List<DrivePhotoFile>();
        string? page = null;
        do
        {
            var req = _service.Files.List();
            req.Q = $"'{folderId.Replace("'", "")}' in parents and trashed = false and mimeType contains 'image/'";
            req.Fields = "nextPageToken, files(id, name, md5Checksum, modifiedTime, size)";
            req.PageSize = 1000;
            req.PageToken = page;
            req.SupportsAllDrives = true;
            req.IncludeItemsFromAllDrives = true;
            var res = await req.ExecuteAsync(ct);
            files.AddRange(res.Files.Select(f => new DrivePhotoFile(f.Id, f.Name, f.Md5Checksum, f.ModifiedTimeDateTimeOffset, f.Size)));
            page = res.NextPageToken;
        } while (page is not null);
        return files;
    }

    public async Task<byte[]> DownloadAsync(string fileId, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var req = _service.Files.Get(fileId);
        req.SupportsAllDrives = true;
        var progress = await req.DownloadAsync(ms, ct);
        if (progress.Exception is not null) throw progress.Exception;
        return ms.ToArray();
    }

    public void Dispose() => _service.Dispose();
}

/// <summary>Resultado de una pasada: qué cambió y lo que no se pudo usar (para el log).</summary>
public sealed record DrivePhotoSyncResult(int Added, int Updated, int Removed, int Total, IReadOnlyList<string> Problems)
{
    public bool Changed => Added + Updated + Removed > 0;
}

/// <summary>
/// Copia la carpeta de fotos de Drive del cliente a <c>{dir}/fotos/CLAVE.webp</c>. El nombre del archivo es el OEM
/// (o el código del local si no tiene); la clave es la misma de <see cref="ProductImages.Key"/>.
/// Solo baja lo nuevo o cambiado (md5 de Drive), y si el cliente borra una foto en Drive, también sale de la página.
/// Si dos archivos dan la misma clave, gana el modificado más recientemente.
/// </summary>
public sealed class DrivePhotoSync(IDrivePhotoSource drive, string dir)
{
    public const string PhotosSubdir = "fotos";
    private const long MaxBytes = 25 * 1024 * 1024;
    private const long Failed = -1;
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private sealed record Entry(string FileId, string Name, string? Md5, long Written);

    public async Task<DrivePhotoSyncResult> RunAsync(string folderId, CancellationToken ct)
    {
        var photosDir = Path.Combine(dir, PhotosSubdir);
        Directory.CreateDirectory(photosDir);
        var statePath = Path.Combine(dir, "estado.json");
        var state = LoadState(statePath);
        var problems = new List<string>();

        var byKey = new Dictionary<string, DrivePhotoFile>(StringComparer.Ordinal);
        foreach (var f in await drive.ListAsync(folderId, ct))
        {
            var key = ProductImages.Key(Path.GetFileNameWithoutExtension(f.Name));
            if (key is null) { problems.Add($"{f.Name}: el nombre no tiene letras ni números"); continue; }
            if (byKey.TryGetValue(key, out var other))
            {
                var keep = (f.Modified ?? DateTimeOffset.MinValue) > (other.Modified ?? DateTimeOffset.MinValue) ? f : other;
                problems.Add($"{f.Name} y {other.Name} son la misma foto ({key}); se usa {keep.Name}");
                byKey[key] = keep;
            }
            else byKey[key] = f;
        }

        int added = 0, updated = 0, removed = 0, failed = 0;
        foreach (var (key, f) in byKey)
        {
            var target = Path.Combine(photosDir, key + ".webp");
            var same = state.TryGetValue(key, out var e) && e.FileId == f.Id && e.Md5 == f.Md5 && f.Md5 is not null;
            if (same && e!.Written == Failed) continue; // ya se avisó; se reintenta cuando cambien el archivo
            if (same && File.Exists(target) && new FileInfo(target).Length == e!.Written) continue;

            string? problem = null;
            byte[]? webp = null;
            if (f.Size > MaxBytes) problem = "pesa más de 25 MB";
            else
            {
                try { webp = PhotoProcessor.Process(await drive.DownloadAsync(f.Id, ct)); }
                catch (Exception ex) when (ex is not OperationCanceledException and not IOException) { problem = ex.Message; }
                if (webp is null) problem ??= "no se pudo leer la imagen (¿formato HEIC?; súbanla en JPG o PNG)";
            }
            if (problem is not null)
            {
                // Si ya había una versión buena, se queda en la página hasta que suban una que sirva.
                problems.Add($"{f.Name}: {problem}");
                state[key] = new Entry(f.Id, f.Name, f.Md5, Failed);
                failed++;
                continue;
            }

            var tmp = target + ".tmp";
            await File.WriteAllBytesAsync(tmp, webp!, ct);
            File.Move(tmp, target, overwrite: true);
            if (state.ContainsKey(key)) updated++; else added++;
            state[key] = new Entry(f.Id, f.Name, f.Md5, webp!.Length);
        }

        foreach (var key in state.Keys.Where(k => !byKey.ContainsKey(k)).ToList())
        {
            File.Delete(Path.Combine(photosDir, key + ".webp"));
            state.Remove(key);
            removed++;
        }

        if (added + updated + removed + failed > 0)
        {
            var tmp = statePath + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(state, Json), ct);
            File.Move(tmp, statePath, overwrite: true);
        }
        return new DrivePhotoSyncResult(added, updated, removed, state.Values.Count(v => v.Written > 0), problems);
    }

    private static Dictionary<string, Entry> LoadState(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(path)) ?? [];
        }
        catch (JsonException) { }
        return new Dictionary<string, Entry>(StringComparer.Ordinal);
    }
}
