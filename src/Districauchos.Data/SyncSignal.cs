using Npgsql;

namespace Districauchos.Data;

/// <summary>
/// Aviso "sincroniza ya" de la API al Worker por PostgreSQL (LISTEN/NOTIFY): son procesos separados
/// y así no hace falta abrir un puerto en el Worker.
/// </summary>
public static class SyncSignal
{
    public const string Channel = "districauchos_sync";

    public static async Task RequestAsync(NpgsqlDataSource db, CancellationToken ct)
    {
        await using var cmd = db.CreateCommand($"NOTIFY {Channel}");
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Escucha avisos y llama a <paramref name="onRequest"/>. Se reconecta si se cae la conexión.</summary>
    public static Task ListenAsync(NpgsqlDataSource db, Action onRequest, Action<Exception> onError, CancellationToken ct) =>
        ListenAsync(db, Channel, onRequest, onError, ct);

    /// <summary>Igual, en otro canal (p. ej. <see cref="StockCheckRepository.Channel"/>).</summary>
    public static async Task ListenAsync(NpgsqlDataSource db, string channel, Action onRequest, Action<Exception> onError, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var conn = await db.OpenConnectionAsync(ct);
                conn.Notification += (_, _) => onRequest();
                await using (var cmd = new NpgsqlCommand($"LISTEN {channel}", conn))
                    await cmd.ExecuteNonQueryAsync(ct);
                while (!ct.IsCancellationRequested)
                    await conn.WaitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                onError(ex);
                try { await Task.Delay(TimeSpan.FromSeconds(30), ct); } catch (OperationCanceledException) { return; }
            }
        }
    }
}
