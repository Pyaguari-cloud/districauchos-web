using Districauchos.Core;

namespace Districauchos.Sheets;

public static class SheetSourceFactory
{
    /// <summary>CSV locales si LOCAL_CSV_DIR está definido; si no, Google Sheets (solo lectura).</summary>
    public static ISheetSource Create(AppSettings s)
    {
        if (!string.IsNullOrWhiteSpace(s.LocalCsvDir)) return new CsvSheetSource(s.LocalCsvDir);
        if (string.IsNullOrWhiteSpace(s.GoogleServiceAccountJsonPath))
            throw new InvalidOperationException("Define GOOGLE_SERVICE_ACCOUNT_JSON_PATH o LOCAL_CSV_DIR");
        return new GoogleSheetSource(s.GoogleServiceAccountJsonPath);
    }
}
