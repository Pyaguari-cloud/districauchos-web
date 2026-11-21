using Districauchos.Core;
using Districauchos.Data;
using Districauchos.Sheets;
using Npgsql;

namespace Districauchos.Worker;

/// <summary>
/// Revisa la carpeta de fotos de Drive del cliente cada DRIVE_PHOTOS_INTERVAL_MINUTES (5 por defecto).
/// Si algo cambió (foto nueva, reemplazada o borrada), pide una sincronización para que salga en la página
/// sin esperar la hora. Sin DRIVE_FOLDER_FOTOS no hace nada.
/// </summary>
public sealed class DrivePhotoService(AppSettings settings, NpgsqlDataSource db, ILogger<DrivePhotoService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(settings.DriveFolderFotos) || string.IsNullOrWhiteSpace(settings.GoogleServiceAccountJsonPath)
            || !File.Exists(settings.GoogleServiceAccountJsonPath))
        {
            log.LogInformation("Fotos de Drive desactivadas (falta DRIVE_FOLDER_FOTOS o la cuenta de servicio)");
            return;
        }

        using var drive = new GoogleDrivePhotoSource(settings.GoogleServiceAccountJsonPath);
        var sync = new DrivePhotoSync(drive, settings.DrivePhotosDir);
        var interval = TimeSpan.FromMinutes(settings.DrivePhotosIntervalMinutes);
        var first = true;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var r = await sync.RunAsync(settings.DriveFolderFotos, stoppingToken);
                foreach (var p in r.Problems) log.LogWarning("Foto de Drive no usada: {Problem}", p);
                if (r.Changed || first)
                    log.LogInformation("Fotos de Drive: {Added} nuevas, {Updated} reemplazadas, {Removed} quitadas ({Total} en total)",
                        r.Added, r.Updated, r.Removed, r.Total);
                // Si hay una sincronización en curso, el aviso queda anotado y corre otra al terminar.
                if (r.Changed) await SyncSignal.RequestAsync(db, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "No se pudo revisar la carpeta de fotos de Drive; reintento en {Minutes} min", interval.TotalMinutes);
            }
            first = false;
            try { await Task.Delay(interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
