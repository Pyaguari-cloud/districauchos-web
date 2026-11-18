namespace Districauchos.Sheets;

/// <summary>Fuente de solo lectura de una pestaña completa como matriz de celdas de texto.</summary>
public interface ISheetSource
{
    Task<IReadOnlyList<IReadOnlyList<string>>> ReadTabAsync(string spreadsheetId, string tab, CancellationToken ct = default);
}
