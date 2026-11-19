using System.Security.Cryptography;
using Districauchos.Core;
using Districauchos.Core.Orders;
using Districauchos.Core.Pricing;
using Districauchos.Data;

namespace Districauchos.Api.Payments;

public sealed record OrderItemInput(string? Oem, string? Code, int Qty);
public sealed record CreateOrderRequest(List<OrderItemInput>? Items, string? Vehicle, string? Delivery, CustomerInput? Customer);
public sealed record ConfirmOrderRequest(long Id, string? ClientTransactionId);

/// <summary>
/// Pago en línea con PayPhone. Precios, stock y montos los fija siempre el servidor; el navegador
/// solo manda OEM, cantidad, entrega y datos de contacto.
/// </summary>
public static class OrderEndpoints
{
    public static bool Enabled(AppSettings s) => s.PaymentsMode is "payphone" or "fake";

    public static void AddPayments(this WebApplicationBuilder builder, AppSettings s)
    {
        builder.Services.AddSingleton<OrderRepository>();
        builder.Services.AddSingleton<OrderNotifier>();
        switch (s.PaymentsMode)
        {
            case "payphone":
                if (string.IsNullOrWhiteSpace(s.PayPhoneToken))
                    throw new InvalidOperationException("PAYMENTS_MODE=payphone necesita PAYPHONE_TOKEN en .env");
                builder.Services.AddHttpClient<IPaymentGateway, PayPhoneGateway>(http => PayPhoneGateway.Configure(http, s));
                break;
            case "fake":
                if (!FakeGateway.IsLocal(s.SiteUrl))
                    throw new InvalidOperationException("PAYMENTS_MODE=fake solo se permite con SITE_URL en localhost");
                builder.Services.AddSingleton<IPaymentGateway, FakeGateway>();
                break;
        }
    }

    public static void MapOrders(this WebApplication app, AppSettings s)
    {
        app.MapGet("/api/orders/config", () => Results.Ok(new
        {
            enabled = Enabled(s),
            mode = s.PaymentsMode,
            backorderDays = s.BackorderDays,
            consumidorFinalMax = s.ConsumidorFinalMax,
        }));
        if (!Enabled(s)) return;

        app.MapPost("/api/orders", async (
            CreateOrderRequest req, QuoteCalculator calc, CatalogQueries queries, OrderRepository orders,
            IPaymentGateway gateway, ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("Orders");
            if (!Enum.TryParse<Delivery>(req.Delivery, ignoreCase: true, out var delivery) || !Enum.IsDefined(delivery) || delivery == Delivery.None)
                return Problem("Elige cómo recibes el pedido.", "delivery");

            var lines = (req.Items ?? [])
                .Where(i => !string.IsNullOrWhiteSpace(i.Oem) && i.Oem.Length <= 64 && (i.Code?.Length ?? 0) <= 64)
                .Take(QuoteCalculator.MaxLines)
                .Select(i => new QuoteRequestLine(i.Oem!.Trim(), i.Code?.Trim(), i.Qty))
                .ToList();
            if (lines.Count == 0) return Problem("El carrito está vacío.");

            var variants = await queries.GetQuoteVariantsAsync(lines.Select(l => l.Oem).Distinct().ToList(), ct);
            var vehicle = req.Vehicle is { Length: > 0 and <= 80 } v ? v.Trim() : null;
            var quote = calc.Calculate(lines, variants, vehicle, delivery);
            if (quote.Missing.Count > 0)
                return Problem("Algunos repuestos ya no están en el catálogo. Quítalos del carrito para continuar.");
            if (quote.Lines.Count == 0) return Problem("El carrito está vacío.");
            if (s.BackorderDays == 0 && quote.Lines.Any(l => !l.Available))
                return Problem("Hay repuestos sin stock en el carrito. Quítalos o cotízalos por WhatsApp.");

            var (customer, errors) = CustomerValidator.Validate(req.Customer ?? new(null, null, null, null, null, null, null, null),
                delivery, quote.GrandTotal, s.ConsumidorFinalMax);
            if (customer is null) return Results.Json(new { error = "Revisa los datos marcados.", fields = errors }, statusCode: 400);

            var orderLines = quote.Lines.Select(l => new OrderLine(
                l.Oem, l.Code, l.Title, l.Qty, l.UnitPrice, l.LineTotal, l.MayorApplied, Backorder: !l.Available, l.ShowCode)).ToList();
            var token = NewToken();
            var number = await orders.CreateAsync(token, customer, delivery, orderLines, quote.Total, quote.Shipping, quote.GrandTotal,
                quote.MayorApplied, vehicle, s.BackorderDays, ct);
            var order = (await orders.GetByTokenAsync(token, ct))!;

            try
            {
                var prepared = await gateway.PrepareAsync(order, PaymentAmounts.FromTotal(order.Total, s.IvaRate), ct);
                log.LogInformation("Pedido #{Number} creado ({Total}); enviado a PayPhone", number, order.Total);
                return Results.Ok(new { number, payUrl = prepared.PayUrl });
            }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or TaskCanceledException)
            {
                log.LogError(e, "Pedido #{Number}: no se pudo preparar el pago", number);
                await orders.TryCompleteAsync(token, OrderStatus.Failed, null, null, e.Message, CancellationToken.None);
                return Results.Json(new { error = "No pudimos conectar con PayPhone. Inténtalo en unos minutos o cotiza por WhatsApp." }, statusCode: 502);
            }
        });

        // PayPhone devuelve al cliente con id + clientTransactionId; aquí se verifica con PayPhone (no con el navegador).
        app.MapPost("/api/orders/confirm", async (
            ConfirmOrderRequest req, OrderRepository orders, IPaymentGateway gateway, OrderNotifier notifier,
            ILoggerFactory lf, CancellationToken ct) =>
        {
            var log = lf.CreateLogger("Orders");
            if (string.IsNullOrWhiteSpace(req.ClientTransactionId) || req.ClientTransactionId.Length > 64) return Results.NotFound();
            var order = await orders.GetByTokenAsync(req.ClientTransactionId, ct);
            if (order is null) return Results.NotFound();
            if (order.Status != OrderStatus.Pending) return Results.Ok(Summary(order)); // recargar la página no confirma dos veces

            var expected = PaymentAmounts.FromTotal(order.Total, s.IvaRate).Amount;
            ConfirmResult r;
            try { r = await gateway.ConfirmAsync(req.Id, order.Token, expected, ct); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
            {
                log.LogError(e, "Pedido #{Number}: PayPhone no respondió la confirmación", order.Number);
                return Results.Json(new { error = "No pudimos confirmar el pago todavía. Recarga la página en unos segundos." }, statusCode: 502);
            }

            OrderStatus status;
            if (r.Approved && r.AmountCents == expected && r.ClientTransactionId == order.Token) status = OrderStatus.Paid;
            else if (r.Approved)
            {
                // Pagado pero no cuadra: no se despacha automáticamente, lo revisa una persona.
                status = OrderStatus.Review;
                log.LogCritical("Pedido #{Number}: pago aprobado que NO cuadra (monto {Got} vs {Expected}, tx {Tx})",
                    order.Number, r.AmountCents, expected, r.TransactionId);
            }
            else status = r.Canceled ? OrderStatus.Canceled : OrderStatus.Failed;

            if (await orders.TryCompleteAsync(order.Token, status, r.TransactionId, r.AuthorizationCode, r.Message, ct))
            {
                order = (await orders.GetByTokenAsync(order.Token, ct))!;
                log.LogInformation("Pedido #{Number}: {Status}", order.Number, status);
                if (status is OrderStatus.Paid or OrderStatus.Review && await notifier.NotifyPaidAsync(order, CancellationToken.None))
                    await orders.MarkNotifiedAsync(order.Token, CancellationToken.None);
            }
            else order = (await orders.GetByTokenAsync(order.Token, ct))!;
            return Results.Ok(Summary(order));
        });
    }

    /// <summary>Lo que ve el comprador en "pago recibido". Sin cédula ni datos de tarjeta.</summary>
    private static object Summary(Order o) => new
    {
        number = o.Number,
        status = o.Status.ToString().ToLowerInvariant(),
        subtotal = o.Subtotal,
        shipping = o.Shipping,
        total = o.Total,
        delivery = OrderText.DeliveryText(o.Delivery, o.Customer.City),
        backorderDays = o.BackorderDays,
        hasBackorder = o.Lines.Any(l => l.Backorder),
        lines = o.Lines.Select(l => new { l.Oem, l.Title, l.Qty, l.LineTotal, l.Backorder }),
        name = o.Customer.Name,
        email = o.Customer.Email,
        whatsappText = OrderText.ForCustomerWhatsApp(o),
    };

    private static IResult Problem(string error, string? field = null) =>
        Results.Json(field is null ? new { error } : new { error, fields = new Dictionary<string, string> { [field] = error } }, statusCode: 400);

    /// <summary>clientTransactionId: 128 bits aleatorios en base64url (no se puede adivinar el pedido de otro).</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
