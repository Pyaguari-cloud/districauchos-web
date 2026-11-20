using Districauchos.Core.Catalog;
using System.Text;
using Districauchos.Core.Pricing;

namespace Districauchos.Core.Orders;

public enum OrderStatus { Pending, Paid, Canceled, Failed, Review }

/// <summary>Línea del pedido tal como se cobró (precio fijado por el servidor al crear el pedido).</summary>
public sealed record OrderLine(
    string Oem, string Code, string Title, int Qty, decimal UnitPrice, decimal LineTotal, bool MayorApplied, bool Backorder, bool ShowCode,
    /// <summary>Unidades que había en inventario al pagar (pedidos viejos: -1 = no se registró).</summary>
    int InStock = -1)
{
    /// <summary>"[BAJO PEDIDO ~4 días]" o "[3 en stock + 2 BAJO PEDIDO ~4 días]".</summary>
    public string? BackorderNote(int days) =>
        !Backorder ? null
        : InStock > 0 ? $"[{InStock} en stock + {Qty - InStock} BAJO PEDIDO ~{days} días]"
        : $"[BAJO PEDIDO ~{days} días]";
}

/// <summary>Pedido pagado en línea. <c>Token</c> = clientTransactionId de PayPhone (aleatorio, no adivinable).</summary>
public sealed record Order(
    long Number, string Token, OrderStatus Status, DateTimeOffset CreatedAt, Customer Customer, Delivery Delivery,
    IReadOnlyList<OrderLine> Lines, decimal Subtotal, decimal Shipping, decimal Total, bool MayorApplied,
    string? Vehicle, int BackorderDays, long? PaymentId, string? AuthorizationCode);

/// <summary>Montos en centavos como los pide PayPhone: amount = amountWithTax + tax (los precios ya incluyen IVA).</summary>
public readonly record struct PaymentAmounts(int Amount, int AmountWithTax, int Tax)
{
    public static PaymentAmounts FromTotal(decimal total, decimal ivaRate)
    {
        var cents = (int)Math.Round(total * 100m, MidpointRounding.AwayFromZero);
        var taxBase = (int)Math.Round(cents / (1m + ivaRate), MidpointRounding.AwayFromZero);
        return new(cents, taxBase, cents - taxBase);
    }
}

public static class OrderText
{
    private static readonly Dictionary<DocType, string> DocNames = new()
    {
        [DocType.Cedula] = "Cédula", [DocType.Ruc] = "RUC", [DocType.Pasaporte] = "Pasaporte",
    };

    public static string DeliveryText(Delivery d, string? city) => d switch
    {
        Delivery.Pickup => "Retiro en local",
        Delivery.Quito => "Envío dentro de Quito (sin costo)",
        Delivery.National => $"Envío nacional por cooperativa o Servientrega a {city}",
        Delivery.Galapagos => $"Envío a Galápagos por avión a {city}",
        _ => "Sin definir",
    };

    /// <summary>Aviso para el local (correo y WhatsApp): todo lo necesario para facturar en el POS y despachar.</summary>
    public static string ForStore(Order o)
    {
        var c = o.Customer;
        var sb = new StringBuilder();
        sb.Append($"PEDIDO #{o.Number} – PAGADO con PayPhone\n");
        if (o.PaymentId is not null) sb.Append($"Transacción PayPhone: {o.PaymentId} · Autorización: {o.AuthorizationCode}\n");
        sb.Append('\n');
        for (var i = 0; i < o.Lines.Count; i++)
        {
            var l = o.Lines[i];
            var id = CatalogKey.Label(l.Oem, l.Code, l.ShowCode);
            sb.Append($"{i + 1}) {id} – {l.Title} – Cant: {l.Qty} – {QuoteCalculator.Money(l.UnitPrice)} c/u = {QuoteCalculator.Money(l.LineTotal)}");
            if (l.MayorApplied) sb.Append(" (por mayor)");
            if (l.BackorderNote(o.BackorderDays) is { } note) sb.Append($" {note}");
            sb.Append('\n');
        }
        sb.Append($"\nSubtotal: {QuoteCalculator.Money(o.Subtotal)}\n");
        if (o.Shipping > 0) sb.Append($"Envío: {QuoteCalculator.Money(o.Shipping)}\n");
        sb.Append($"TOTAL PAGADO: {QuoteCalculator.Money(o.Total)} (IVA incluido)\n\n");
        sb.Append($"Entrega: {DeliveryText(o.Delivery, c.City)}\n");
        if (c.Address is not null) sb.Append($"Dirección: {c.Address}\n");
        if (!string.IsNullOrWhiteSpace(o.Vehicle)) sb.Append($"Vehículo: {o.Vehicle}\n");
        sb.Append("\nFACTURAR A\n");
        sb.Append($"Nombre: {c.Name}\n");
        sb.Append($"{DocNames[c.DocType]}: {c.DocNumber}\n");
        sb.Append($"Celular: {c.Phone}\nCorreo: {c.Email}\n");
        if (c.Notes is not null) sb.Append($"\nNota del cliente: {c.Notes}\n");
        return sb.ToString();
    }

    /// <summary>Mensaje corto que el comprador envía por WhatsApp desde la pantalla de pago recibido.</summary>
    public static string ForCustomerWhatsApp(Order o)
    {
        var sb = new StringBuilder($"Hola, acabo de pagar el pedido #{o.Number} en la página ({QuoteCalculator.Money(o.Total)}).\n");
        foreach (var l in o.Lines) sb.Append($"- {CatalogKey.Label(l.Oem, l.Code, l.ShowCode)} {l.Title} x{l.Qty}{(l.Backorder ? (l.InStock > 0 ? $" ({l.Qty - l.InStock} bajo pedido)" : " (bajo pedido)") : "")}\n");
        sb.Append($"Entrega: {DeliveryText(o.Delivery, o.Customer.City)}\n");
        sb.Append($"A nombre de: {o.Customer.Name}");
        return sb.ToString();
    }
}
