using System.Net;
using System.Net.Mail;
using Districauchos.Core;

namespace Districauchos.Api.Accounts;

/// <summary>Correo de "olvidé mi contraseña" por el mismo SMTP de los avisos de pedidos (Brevo).</summary>
public sealed class AccountMailer(AppSettings settings, ILogger<AccountMailer> log)
{
    public bool Configured => !string.IsNullOrWhiteSpace(settings.SmtpHost) && !string.IsNullOrWhiteSpace(settings.SmtpFrom ?? settings.SmtpUser);

    /// <summary>El token va después de # : el navegador no lo manda al servidor ni lo deja en registros o en el Referer.</summary>
    public string ResetUrl(string token) => $"{settings.SiteUrl}/cuenta/restablecer#t={token}";

    public async Task<bool> SendPasswordResetAsync(string email, string name, string token, CancellationToken ct)
    {
        if (!Configured)
        {
            log.LogWarning("Recuperación de contraseña pedida, pero SMTP no está configurado");
            return false;
        }
        var first = name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var body = $"""
            Hola {first}:

            Recibimos un pedido para cambiar la contraseña de tu cuenta en Districauchos VCH.
            Para elegir una contraseña nueva, abre este enlace (vale 30 minutos y sirve una sola vez):

            {ResetUrl(token)}

            Si no fuiste tú, ignora este correo: tu contraseña sigue igual.

            Districauchos VCH · {settings.SiteUrl}
            """;
        try
        {
            using var smtp = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                EnableSsl = true,
                Credentials = string.IsNullOrEmpty(settings.SmtpUser) ? null : new NetworkCredential(settings.SmtpUser, settings.SmtpPass),
                Timeout = 15000,
            };
            using var mail = new MailMessage(settings.SmtpFrom ?? settings.SmtpUser!, email, "Cambia tu contraseña · Districauchos VCH", body);
            await smtp.SendMailAsync(mail, ct);
            log.LogInformation("Correo de recuperación enviado");
            return true;
        }
        catch (Exception e) when (e is SmtpException or InvalidOperationException or FormatException or OperationCanceledException)
        {
            log.LogError(e, "No se pudo enviar el correo de recuperación");
            return false;
        }
    }
}
