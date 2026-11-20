using Districauchos.Core;
using Districauchos.Core.Catalog;
using Districauchos.Core.Model;
using Districauchos.Data;
using Districauchos.Search;
using Districauchos.Sheets;
using Npgsql;

namespace Districauchos.Worker;

/// <summary>
/// Revisión de stock al pagar (ver <see cref="StockCheckRepository"/>). Lee solo las 4 pestañas de stock y
/// actualiza los códigos pedidos en Postgres y en el buscador.
/// Para no gastar la cuota de Google, una lectura de hace menos de <see cref="ReuseFor"/> se reutiliza:
/// varias personas pagando a la vez = una sola lectura.
/// </summary>
public sealed class StockCheckService(
    AppSettings settings,
    ISheetSource source,
    StockCheckRepository checks,
    ProductIndex index,
    NpgsqlDataSource db,
    ILogger<StockCheckService> log) : BackgroundService
{
    private static readonly TimeSpan ReuseFor = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(15);

    private readonly SemaphoreSlim _signal = new(0);
    private IReadOnlyDictionary<string, decimal>? _lastTotals;
    private DateTime _lastReadUtc = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await checks.EnsureSchemaAsync(stoppingToken);
        var listener = SyncSignal.ListenAsync(db, StockCheckRepository.Channel, () => _signal.Release(),
            ex => log.LogWarning(ex, "Escucha de revisiones de stock caída; reintento en 30 s"), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Espera un aviso; cada 5 s revisa igual por si un aviso se perdió.
            try { await _signal.WaitAsync(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) { break; }
            while (_signal.CurrentCount > 0) _signal.Wait(0); // varios avisos seguidos = una pasada

            try { await ProcessPendingAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Revisión de stock falló");
            }
        }
        await listener;
    }

    private async Task ProcessPendingAsync(CancellationToken ct)
    {
        var (ids, codes) = await checks.TakePendingAsync(ct);
        if (ids.Length == 0) return;

        try
        {
            var totals = await GetTotalsAsync(ct);
            var changes = await checks.ApplyAsync(codes, totals, ct);
            // El buscador es secundario: si falla, la ficha y el pago ya usan el dato nuevo de Postgres.
            try { await index.UpdateStockAsync(changes.Select(c => (c.Slug, c.Stock, c.Available)), ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Revisión de stock: no se pudo actualizar el buscador");
            }
            await checks.CompleteAsync(ids, true, null, ct);
            log.LogInformation("Revisión de stock: {Codes} códigos, {Changed} fichas cambiaron", codes.Length, changes.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await checks.CompleteAsync(ids, false, ex.Message, CancellationToken.None);
            throw;
        }
    }

    private async Task<IReadOnlyDictionary<string, decimal>> GetTotalsAsync(CancellationToken ct)
    {
        if (_lastTotals is not null && DateTime.UtcNow - _lastReadUtc < ReuseFor) return _lastTotals;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ReadTimeout);
        IReadOnlyList<StockRow> rows = await new SheetLoader(source, settings).LoadStockAsync(timeout.Token);
        _lastTotals = CatalogBuilder.PositiveTotals(rows);
        _lastReadUtc = DateTime.UtcNow;
        return _lastTotals;
    }
}
