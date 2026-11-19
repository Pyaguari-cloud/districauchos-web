using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Districauchos.Core;
using Districauchos.Core.Orders;

namespace Districauchos.Api.Payments;

public sealed record PrepareResult(string PayUrl);

/// <summary>Resultado verificado con la pasarela (nunca con lo que dice el navegador).</summary>
public sealed record ConfirmResult(bool Approved, bool Canceled, int AmountCents, string? ClientTransactionId, long? TransactionId,
    string? AuthorizationCode, string? Message);

public interface IPaymentGateway
{
    Task<PrepareResult> PrepareAsync(Order order, PaymentAmounts amounts, CancellationToken ct);
    Task<ConfirmResult> ConfirmAsync(long id, string clientTransactionId, int expectedCents, CancellationToken ct);
}

/// <summary>
/// PayPhone "Botón de Pagos por redirección" (docs.payphone.app). El cliente paga en la página de PayPhone
/// (tarjeta o app) y vuelve a {SITE_URL}/pago/respuesta?id=…&amp;clientTransactionId=…; ahí se confirma.
/// Si no se confirma en 5 minutos, PayPhone reversa el cobro solo.
/// </summary>
public sealed class PayPhoneGateway(HttpClient http, AppSettings settings, ILogger<PayPhoneGateway> log) : IPaymentGateway
{
    public static void Configure(HttpClient http, AppSettings s)
    {
        http.BaseAddress = new Uri(s.PayPhoneApiUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromSeconds(20);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", s.PayPhoneToken);
    }

    public async Task<PrepareResult> PrepareAsync(Order order, PaymentAmounts a, CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["amount"] = a.Amount,
            ["amountWithoutTax"] = 0,
            ["amountWithTax"] = a.AmountWithTax,
            ["tax"] = a.Tax,
            ["service"] = 0,
            ["tip"] = 0,
            ["currency"] = "USD",
            ["clientTransactionId"] = order.Token,
            ["reference"] = $"Pedido {order.Number} Districauchos",
            ["responseUrl"] = $"{settings.SiteUrl}/pago/respuesta",
            ["cancellationUrl"] = $"{settings.SiteUrl}/carrito?pago=cancelado",
            ["email"] = order.Customer.Email,
            ["phoneNumber"] = order.Customer.Phone,
            ["lang"] = "es",
            ["timeZone"] = -5,
        };
        if (!string.IsNullOrWhiteSpace(settings.PayPhoneStoreId)) body["storeId"] = settings.PayPhoneStoreId;

        using var res = await http.PostAsync("api/button/Prepare", JsonContent(body), ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            log.LogError("PayPhone Prepare {Status} (pedido {Number}): {Body}", (int)res.StatusCode, order.Number, Short(text));
            throw new InvalidOperationException("PayPhone rechazó la preparación del pago");
        }
        var json = JsonNode.Parse(text);
        var url = json?["payWithCard"]?.GetValue<string>();
        if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.Ordinal))
            throw new InvalidOperationException("PayPhone no devolvió el enlace de pago");
        return new PrepareResult(url);
    }

    public async Task<ConfirmResult> ConfirmAsync(long id, string clientTransactionId, int expectedCents, CancellationToken ct)
    {
        var body = new JsonObject { ["id"] = id, ["clientTxId"] = clientTransactionId };
        using var res = await http.PostAsync("api/button/V2/Confirm", JsonContent(body), ct);
        var text = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            log.LogWarning("PayPhone Confirm {Status} (tx {Id}): {Body}", (int)res.StatusCode, id, Short(text));
            return new ConfirmResult(false, false, 0, null, null, null, Message(text));
        }
        var j = JsonNode.Parse(text)!;
        var status = j["statusCode"]?.GetValue<int>() ?? 0;
        return new ConfirmResult(
            Approved: status == 3 && string.Equals(j["transactionStatus"]?.GetValue<string>(), "Approved", StringComparison.OrdinalIgnoreCase),
            Canceled: status == 2,
            AmountCents: j["amount"]?.GetValue<int>() ?? 0,
            ClientTransactionId: j["clientTransactionId"]?.GetValue<string>(),
            TransactionId: j["transactionId"]?.GetValue<long>(),
            AuthorizationCode: j["authorizationCode"]?.ToString(),
            Message: j["message"]?.ToString());
        // A propósito no se guardan bin, últimos dígitos ni marca de la tarjeta.
    }

    private static StringContent JsonContent(JsonNode body) => new(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
    private static string Short(string s) => s.Length > 500 ? s[..500] : s;
    private static string? Message(string text)
    {
        try { return JsonNode.Parse(text)?["message"]?.ToString(); } catch (JsonException) { return null; }
    }
}

/// <summary>
/// Simulador para probar el flujo completo sin token (PAYMENTS_MODE=fake). Solo arranca si SITE_URL es localhost.
/// id 1 = aprobado, cualquier otro = cancelado.
/// </summary>
public sealed class FakeGateway(AppSettings settings) : IPaymentGateway
{
    public Task<PrepareResult> PrepareAsync(Order order, PaymentAmounts a, CancellationToken ct) =>
        Task.FromResult(new PrepareResult($"{settings.SiteUrl}/pago/prueba?tx={Uri.EscapeDataString(order.Token)}&monto={a.Amount}"));

    public Task<ConfirmResult> ConfirmAsync(long id, string clientTransactionId, int expectedCents, CancellationToken ct) =>
        Task.FromResult(id == 1
            ? new ConfirmResult(true, false, expectedCents, clientTransactionId, 900000 + Random.Shared.Next(99999), "PRUEBA", "Aprobada (simulador)")
            : new ConfirmResult(false, true, 0, clientTransactionId, null, null, "Cancelada (simulador)"));

    public static bool IsLocal(string siteUrl) =>
        Uri.TryCreate(siteUrl, UriKind.Absolute, out var u) && (u.IsLoopback || u.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase));
}
