using Districauchos.Core;
using Districauchos.Data;
using Districauchos.Search;
using Districauchos.Sheets;

namespace Districauchos.Worker;

/// <summary>
/// Sincroniza cada SYNC_INTERVAL_MINUTES: Google Sheets (solo lectura) → PostgreSQL.
/// Una corrida fallida no toca el catálogo publicado (la transacción se revierte).
/// </summary>
public sealed class SyncBackgroundService(
    AppSettings settings,
    ISheetSource source,
    CatalogRepository repo,
    ProductIndex index,
    ILogger<SyncBackgroundService> log) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, settings.SyncIntervalMinutes)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunOnceAsync(stoppingToken);
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (!await _gate.WaitAsync(0, ct))
        {
            log.LogWarning("Ya hay una sincronización en curso; se omite esta");
            return;
        }

        long runId = 0;
        CatalogSnapshot? snapshot = null;
        try
        {
            runId = await repo.StartRunAsync(ct);
            var overrides = await repo.LoadPriceOverridesAsync(ct);
            snapshot = await CatalogSnapshot.BuildAsync(source, settings, overrides, ct);

            // Orden: índice temporal → Postgres (transacción) → intercambio del índice.
            // Si falla antes del intercambio, la página sigue con la versión anterior en ambos.
            await index.PrepareAsync(ProductIndex.BuildDocuments(snapshot.Catalog), ct);
            await repo.WriteSnapshotAsync(snapshot.Sheets.Master, snapshot.Sheets.Stock, snapshot.Catalog, ct);
            await index.SwapAsync(ct);
            await repo.FinishRunAsync(runId, "ok", snapshot.Sheets.RowsRead, snapshot.Catalog.Items.Count,
                snapshot.AllWarnings, null, ct);

            var st = snapshot.Catalog.Stats;
            log.LogInformation("Sync {RunId} ok: {Items} fichas ({Available} disponibles), {Vehicles} con vehículo, {Warnings} advertencias",
                runId, st.CatalogItems, st.ItemsAvailable, st.ItemsWithVehicle, snapshot.AllWarnings.Count());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Sync {RunId} falló; se mantiene el catálogo anterior", runId);
            if (runId > 0)
                await repo.FinishRunAsync(runId, "failed", snapshot?.Sheets.RowsRead ?? new Dictionary<string, int>(),
                    null, snapshot?.AllWarnings ?? [], ex.Message, CancellationToken.None);
        }
        finally
        {
            _gate.Release();
        }
    }
}
