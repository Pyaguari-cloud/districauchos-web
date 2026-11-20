using Districauchos.Core;
using Districauchos.Data;
using Districauchos.Search;
using Districauchos.Sheets;
using Npgsql;

namespace Districauchos.Worker;

/// <summary>
/// Sincroniza cada SYNC_INTERVAL_MINUTES: Google Sheets (solo lectura) → PostgreSQL.
/// Una corrida fallida no toca el catálogo publicado (la transacción se revierte).
/// También sincroniza al recibir el aviso de POST /api/admin/sync (<see cref="SyncSignal"/>).
/// </summary>
public sealed class SyncBackgroundService(
    AppSettings settings,
    ISheetSource source,
    CatalogRepository repo,
    ProductIndex index,
    NpgsqlDataSource db,
    ILogger<SyncBackgroundService> log) : BackgroundService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _requested;
    private CancellationTokenSource? _wait;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await repo.EnsureSchemaAsync(stoppingToken);
        var listener = SyncSignal.ListenAsync(db, OnRequest,
            ex => log.LogWarning(ex, "Escucha de avisos de sincronización caída; reintento en 30 s"), stoppingToken);

        var interval = TimeSpan.FromMinutes(Math.Max(1, settings.SyncIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            _requested = false;
            await RunOnceAsync(stoppingToken);
            await WaitAsync(interval, stoppingToken);
        }
        await listener;
    }

    private void OnRequest()
    {
        log.LogInformation("Aviso de sincronización recibido (/api/admin/sync o import-prices)");
        _requested = true;
        try { _wait?.Cancel(); } catch (ObjectDisposedException) { }
    }

    /// <summary>Espera el intervalo o hasta que llegue un aviso (aunque haya llegado durante la corrida).</summary>
    private async Task WaitAsync(TimeSpan interval, CancellationToken stoppingToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _wait = cts;
        try
        {
            if (!_requested) await Task.Delay(interval, cts.Token);
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
        catch (OperationCanceledException) { }
        finally { _wait = null; }
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
