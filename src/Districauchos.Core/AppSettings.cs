using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace Districauchos.Core;

/// <summary>Configuración leída de variables de entorno (.env). Nombres iguales a .env.example.</summary>
public sealed record AppSettings
{
    public string? GoogleServiceAccountJsonPath { get; init; }
    public string? SheetIdPrincipal { get; init; }
    public string? SheetIdMaldonado { get; init; }
    public string? SheetIdBodega { get; init; }
    public string? DriveFolderFotos { get; init; }
    public string? WhatsappNumber { get; init; }
    public decimal MayorDescuento { get; init; } = 0.40m;
    public decimal MayorUmbral { get; init; } = 200m;
    public int SyncIntervalMinutes { get; init; } = 60;
    public string? DatabaseUrl { get; init; }
    public string? AdminToken { get; init; }
    public string VehiclesJsonPath { get; init; } = "data/vehicles.json";
    public string CategoriesJsonPath { get; init; } = "data/categories.json";
    public decimal EnvioNacional { get; init; } = 5m;
    public decimal EnvioGalapagos { get; init; } = 80m;
    public string MeiliUrl { get; init; } = "http://localhost:7700";

    // ---- Pagos en línea (PayPhone, Botón de Pagos por redirección) ----
    /// <summary>off = solo cotización por WhatsApp; payphone = cobro real; fake = simulador local (solo localhost).</summary>
    public string PaymentsMode { get; init; } = "off";
    public string? PayPhoneToken { get; init; }
    public string? PayPhoneStoreId { get; init; }
    public string PayPhoneApiUrl { get; init; } = "https://pay.payphonetodoesposible.com";
    /// <summary>Dirección pública de la web: PayPhone devuelve al cliente a {SiteUrl}/pago/respuesta.</summary>
    public string SiteUrl { get; init; } = "https://districauchosvch.com";
    /// <summary>IVA incluido en los precios (para separar base e impuesto como pide PayPhone).</summary>
    public decimal IvaRate { get; init; } = 0.15m;
    /// <summary>Días de entrega de lo que no está en stock. 0 = no se puede pagar lo que no hay.</summary>
    public int BackorderDays { get; init; } = 4;

    // ---- Aviso de pedidos por correo ----
    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public string? SmtpUser { get; init; }
    public string? SmtpPass { get; init; }
    public string? SmtpFrom { get; init; }
    public string? OrdersEmailTo { get; init; }
    public string? MeiliMasterKey { get; init; }

    /// <summary>
    /// Columna de stock en Tabiazo/Cusubamba/Maldonado cuando hay dos "Stock actual" (pregunta abierta 12.4).
    /// 1 = la primera (columna J), 2 = la segunda (columna M).
    /// </summary>
    public int StockColumnOccurrence { get; init; } = 1;

    /// <summary>Si se define, lee CSV locales (exportados de las hojas) en vez de Google Sheets.</summary>
    public string? LocalCsvDir { get; init; }

    public static AppSettings From(IConfiguration c) => new()
    {
        GoogleServiceAccountJsonPath = c["GOOGLE_SERVICE_ACCOUNT_JSON_PATH"],
        SheetIdPrincipal = c["SHEET_ID_PRINCIPAL"],
        SheetIdMaldonado = c["SHEET_ID_MALDONADO"],
        SheetIdBodega = c["SHEET_ID_BODEGA"],
        DriveFolderFotos = c["DRIVE_FOLDER_FOTOS"],
        WhatsappNumber = c["WHATSAPP_NUMBER"],
        MayorDescuento = Dec(c["MAYOR_DESCUENTO"], 0.40m),
        MayorUmbral = Dec(c["MAYOR_UMBRAL"], 200m),
        EnvioNacional = Dec(c["ENVIO_NACIONAL"], 5m),
        EnvioGalapagos = Dec(c["ENVIO_GALAPAGOS"], 80m),
        SyncIntervalMinutes = int.TryParse(c["SYNC_INTERVAL_MINUTES"], out var m) ? m : 60,
        DatabaseUrl = c["DATABASE_URL"],
        AdminToken = c["ADMIN_TOKEN"],
        VehiclesJsonPath = c["VEHICLES_JSON_PATH"] ?? "data/vehicles.json",
        CategoriesJsonPath = c["CATEGORIES_JSON_PATH"] ?? "data/categories.json",
        MeiliUrl = c["MEILI_URL"] ?? "http://localhost:7700",
        PaymentsMode = (c["PAYMENTS_MODE"] ?? "off").Trim().ToLowerInvariant(),
        PayPhoneToken = c["PAYPHONE_TOKEN"],
        PayPhoneStoreId = c["PAYPHONE_STORE_ID"],
        PayPhoneApiUrl = c["PAYPHONE_API_URL"] ?? "https://pay.payphonetodoesposible.com",
        SiteUrl = (c["SITE_URL"] ?? "https://districauchosvch.com").TrimEnd('/'),
        IvaRate = Dec(c["IVA_RATE"], 0.15m),
        BackorderDays = int.TryParse(c["BACKORDER_DAYS"], out var bd) && bd >= 0 ? bd : 4,
        SmtpHost = c["SMTP_HOST"],
        SmtpPort = int.TryParse(c["SMTP_PORT"], out var sp) ? sp : 587,
        SmtpUser = c["SMTP_USER"],
        SmtpPass = c["SMTP_PASS"],
        SmtpFrom = c["SMTP_FROM"],
        OrdersEmailTo = c["ORDERS_EMAIL_TO"],
        MeiliMasterKey = c["MEILI_MASTER_KEY"],
        StockColumnOccurrence = int.TryParse(c["STOCK_COLUMN_OCCURRENCE"], out var o) && o is 1 or 2 ? o : 1,
        LocalCsvDir = c["LOCAL_CSV_DIR"],
    };

    private static decimal Dec(string? v, decimal fallback) =>
        decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : fallback;
}
