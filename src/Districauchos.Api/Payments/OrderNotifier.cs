using System.Net;
using System.Net.Mail;
using Districauchos.Core;
using Districauchos.Core.Orders;
using Districauchos.Core.Pricing;

namespace Districauchos.Api.Payments;

/// <summary>
/// Aviso de pedido pagado al local por correo (SMTP_*). Si no está configurado solo queda en el log:
/// el local igual ve el pago en PayPhone Business y el comprador puede avisar por WhatsApp.
/// </summary>
public sealed class OrderNotifier(AppSettings settings, ILogger<OrderNotifier> log)
{
    public bool Configured => !string.IsNullOrWhiteSpace(settings.SmtpHost) && !string.IsNullOrWhiteSpace(settings.OrdersEmailTo);

    public async Task<bool> NotifyPaidAsync(Order order, CancellationToken ct)
    {
        var text = OrderText.ForStore(order);
        if (!Configured)
        {
            log.LogWarning("Pedido #{Number} pagado; SMTP sin configurar, no se envió correo", order.Number);
            return false;
        }
        try
        {
            using var smtp = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = true,
                Credentials = string.IsNullOrEmpty(settings.SmtpUser) ? null : new NetworkCredential(settings.SmtpUser, settings.SmtpPass),
                Timeout = 15000,
            };
            using var mail = new MailMessage(
                settings.SmtpFrom ?? settings.SmtpUser ?? settings.OrdersEmailTo!, settings.OrdersEmailTo!,
                $"Pedido #{order.Number} PAGADO – {QuoteCalculator.Money(order.Total)} – {order.Customer.Name}", text);
            mail.ReplyToList.Add(new MailAddress(order.Customer.Email, order.Customer.Name));
            await smtp.SendMailAsync(mail, ct);
            log.LogInformation("Pedido #{Number}: aviso enviado por correo", order.Number);
            return true;
        }
        catch (Exception e) when (e is SmtpException or InvalidOperationException or FormatException or OperationCanceledException)
        {
            log.LogError(e, "Pedido #{Number}: no se pudo enviar el correo de aviso", order.Number);
            return false;
        }
    }
}
