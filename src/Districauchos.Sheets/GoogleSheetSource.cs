using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;

namespace Districauchos.Sheets;

/// <summary>
/// Lee Google Sheets con una cuenta de servicio y scope <c>spreadsheets.readonly</c>.
/// Esta clase solo usa <c>values.get</c>: no existe ningún camino de escritura hacia las hojas del cliente.
/// </summary>
public sealed class GoogleSheetSource : ISheetSource, IDisposable
{
    private readonly SheetsService _service;

    public GoogleSheetSource(string serviceAccountJsonPath)
    {
        if (!File.Exists(serviceAccountJsonPath))
            throw new FileNotFoundException("No se encontró la clave de la cuenta de servicio", serviceAccountJsonPath);

        var credential = CredentialFactory
            .FromFile<ServiceAccountCredential>(serviceAccountJsonPath)
            .ToGoogleCredential()
            .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);

        _service = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "districauchos-catalogo-sync",
        });
    }

    public async Task<IReadOnlyList<IReadOnlyList<string>>> ReadTabAsync(string spreadsheetId, string tab, CancellationToken ct = default)
    {
        var request = _service.Spreadsheets.Values.Get(spreadsheetId, $"'{tab.Replace("'", "''")}'");
        request.ValueRenderOption = SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMATTEDVALUE;
        request.MajorDimension = SpreadsheetsResource.ValuesResource.GetRequest.MajorDimensionEnum.ROWS;

        var response = await request.ExecuteAsync(ct);
        if (response.Values is null) return [];
        return response.Values
            .Select(r => (IReadOnlyList<string>)r.Select(c => c?.ToString() ?? "").ToList())
            .ToList();
    }

    public void Dispose() => _service.Dispose();
}
