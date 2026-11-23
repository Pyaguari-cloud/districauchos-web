using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.RateLimiting;
using Districauchos.Api.Payments;
using Districauchos.Core;
using Districauchos.Core.Accounts;
using Districauchos.Core.Orders;
using Districauchos.Core.Text;
using Districauchos.Data;
using Microsoft.AspNetCore.RateLimiting;

namespace Districauchos.Api.Accounts;

public sealed record RegisterRequest(string? Email, string? Name, string? Password, bool AcceptPrivacy);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record ProfileRequest(string? Name, string? Phone, string? DocType, string? DocNumber, string? Address, string? City);
public sealed record PasswordChangeRequest(string? Current, string? New);
public sealed record ForgotRequest(string? Email);
public sealed record ResetRequest(string? Token, string? Password);
public sealed record DeleteRequest(string? Password);

/// <summary>
/// Cuentas de clientes (pedido del cliente 2026-11-23): correo + contraseña, opcionales. Solo la web (Next.js)
/// llama a estas rutas: guarda el token de sesión en una cookie httpOnly y lo reenvía como "Bearer".
/// La IP real del visitante llega en X-Client-IP (la API no está expuesta a internet).
/// </summary>
public static class AccountEndpoints
{
    private const string Strict = "account-strict";
    private const string Mail = "account-mail";

    public static void AddAccounts(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<AccountRepository>();
        builder.Services.AddSingleton<AccountMailer>();
        builder.Services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (ctx, ct) => await ctx.HttpContext.Response.WriteAsJsonAsync(
                new { error = "Demasiados intentos seguidos. Espera unos minutos e inténtalo de nuevo." }, ct);
            // Ingresar / registrarse: 10 por minuto por IP.
            o.AddPolicy(Strict, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            // Correos de recuperación: 5 por hora por IP.
            o.AddPolicy(Mail, ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromHours(1), QueueLimit = 0 }));
        });
    }

    private static string ClientIp(HttpContext ctx) =>
        ctx.Request.Headers["X-Client-IP"].ToString() is { Length: > 0 and <= 64 } ip ? ip : ctx.Connection.RemoteIpAddress?.ToString() ?? "?";

    private static string NormEmail(string? e) => (e ?? "").Trim().ToLowerInvariant();

    private static string Clean(string? s, int max)
    {
        var t = TextNorm.CollapseSpaces((s ?? "").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ')).Trim();
        return t.Length > max ? t[..max].Trim() : t;
    }

    private static IResult Fields(string error, Dictionary<string, string> fields) =>
        Results.Json(new { error, fields }, statusCode: 400);

    private static IResult Error(string error, int status = 400) => Results.Json(new { error }, statusCode: status);

    private static string? Bearer(HttpRequest r)
    {
        var h = r.Headers.Authorization.ToString();
        var t = h.StartsWith("Bearer ", StringComparison.Ordinal) ? h[7..] : null;
        return AccountTokens.LooksValid(t) ? t : null;
    }

    /// <summary>Cuenta de la sesión que manda la web, o null (sin sesión, vencida o cerrada).</summary>
    public static async Task<Account?> CurrentAsync(HttpRequest r, AccountRepository accounts, CancellationToken ct) =>
        Bearer(r) is { } token ? await accounts.GetBySessionAsync(token, ct) : null;

    private static object Me(Account a) => new
    {
        email = a.Email,
        name = a.Name,
        profile = a.Profile,
        garage = a.Garage,
        createdAt = a.CreatedAt,
    };

    public static void MapAccounts(this WebApplication app, AppSettings s)
    {
        var g = app.MapGroup("/api/account");

        g.MapPost("/register", async (RegisterRequest req, AccountRepository accounts, CancellationToken ct) =>
        {
            var email = NormEmail(req.Email);
            var name = Clean(req.Name, 100);
            var fields = new Dictionary<string, string>();
            if (email.Length > 120 || !CustomerValidator.IsValidEmail(email)) fields["email"] = "Escribe un correo válido.";
            if (name.Length < 3 || !name.Any(char.IsLetter)) fields["name"] = "Escribe tu nombre y apellido.";
            if (Passwords.Problem(req.Password, email) is { } pw) fields["password"] = pw;
            if (!req.AcceptPrivacy) fields["acceptPrivacy"] = "Para crear la cuenta acepta la política de privacidad.";
            if (fields.Count > 0) return Fields("Revisa los datos marcados.", fields);

            var id = await accounts.CreateAsync(email, name, Passwords.Hash(req.Password!), ct);
            if (id is null)
                return Fields("Ya existe una cuenta con ese correo.", new() { ["email"] = "Ya existe una cuenta con ese correo. Ingresa o recupera tu contraseña." });
            var token = await accounts.CreateSessionAsync(id.Value, ct);
            return Results.Ok(new { token, name });
        }).RequireRateLimiting(Strict);

        g.MapPost("/login", async (LoginRequest req, AccountRepository accounts, CancellationToken ct) =>
        {
            var email = NormEmail(req.Email);
            var password = req.Password ?? "";
            if (email.Length == 0 || password.Length == 0) return Error("Escribe tu correo y tu contraseña.");
            if (password.Length > Passwords.MaxLength) return Error("Correo o contraseña incorrectos.", 401);

            var account = await accounts.GetByEmailAsync(email, ct);
            // Mismo tiempo de respuesta exista o no la cuenta: no revela qué correos están registrados.
            var ok = Passwords.Verify(password, account?.PasswordHash ?? Passwords.Dummy);
            if (account is null) return Error("Correo o contraseña incorrectos.", 401);
            if (account.LockedUntil > DateTimeOffset.UtcNow)
                return Error("Por seguridad bloqueamos la cuenta unos minutos por varios intentos fallidos. Espera 15 minutos o cambia tu contraseña con \"¿Olvidaste tu contraseña?\".", 423);
            if (!ok)
            {
                await accounts.RecordFailedLoginAsync(account.Id, ct);
                return Error("Correo o contraseña incorrectos.", 401);
            }
            var token = await accounts.CreateSessionAsync(account.Id, ct);
            return Results.Ok(new { token, name = account.Name });
        }).RequireRateLimiting(Strict);

        g.MapPost("/logout", async (HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
        {
            if (Bearer(http) is { } token) await accounts.DeleteSessionAsync(token, ct);
            return Results.NoContent();
        });

        g.MapGet("/me", async (HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
            await CurrentAsync(http, accounts, ct) is { } a ? Results.Ok(Me(a)) : Results.Unauthorized());

        g.MapPut("/profile", async (ProfileRequest req, HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
        {
            if (await CurrentAsync(http, accounts, ct) is not { } a) return Results.Unauthorized();
            var fields = new Dictionary<string, string>();
            var name = Clean(req.Name, 100);
            if (name.Length < 3 || !name.Any(char.IsLetter)) fields["name"] = "Escribe tu nombre y apellido.";

            string? phone = null;
            if (Clean(req.Phone, 20) is { Length: > 0 } rawPhone && (phone = CustomerValidator.NormalizePhone(rawPhone)) is null)
                fields["phone"] = "Escribe un celular válido, por ejemplo 0987654321.";

            var rawType = (req.DocType ?? "").Trim().ToLowerInvariant();
            var docType = rawType is "cedula" or "ruc" or "pasaporte" ? rawType : null;
            var doc = Clean(req.DocNumber, 20).Replace(" ", "").Replace("-", "");
            if (doc.Length > 0)
            {
                var valid = docType switch
                {
                    "cedula" => CustomerValidator.IsValidCedula(doc),
                    "ruc" => CustomerValidator.IsValidRuc(doc),
                    "pasaporte" => doc.Length is >= 5 and <= 20 && doc.All(char.IsAsciiLetterOrDigit),
                    _ => false,
                };
                if (docType is null) fields["docType"] = "Elige el tipo de documento.";
                else if (!valid) fields["docNumber"] = docType == "ruc" ? "El RUC no es válido (13 dígitos)." : docType == "cedula" ? "La cédula no es válida (10 dígitos)." : "Escribe el número de pasaporte.";
            }
            if (fields.Count > 0) return Fields("Revisa los datos marcados.", fields);

            var profile = new AccountProfile(phone, doc.Length > 0 ? docType : null, doc.Length > 0 ? doc.ToUpperInvariant() : null,
                Clean(req.Address, 200) is { Length: > 0 } ad ? ad : null, Clean(req.City, 60) is { Length: > 0 } ci ? ci : null);
            await accounts.UpdateProfileAsync(a.Id, name, profile, ct);
            return Results.Ok(new { name, profile });
        });

        g.MapPut("/garage", async (HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
        {
            if (await CurrentAsync(http, accounts, ct) is not { } a) return Results.Unauthorized();
            JsonNode? body;
            try { body = await JsonNode.ParseAsync(http.Body, cancellationToken: ct); }
            catch (JsonException) { return Error("Datos inválidos."); }
            if (body is not JsonArray arr) return Error("Datos inválidos.");
            var garage = CleanGarage(arr);
            await accounts.UpdateGarageAsync(a.Id, garage, ct);
            return Results.Ok(garage);
        });

        g.MapGet("/orders", async (HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
        {
            if (await CurrentAsync(http, accounts, ct) is not { } a) return Results.Unauthorized();
            return Results.Ok(OrderEndpoints.Enabled(s) ? await accounts.GetOrdersAsync(a.Id, ct) : []);
        });

        g.MapPut("/password", async (PasswordChangeRequest req, HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
        {
            if (await CurrentAsync(http, accounts, ct) is not { } a) return Results.Unauthorized();
            if (!Passwords.Verify(req.Current ?? "", a.PasswordHash))
                return Fields("La contraseña actual no es correcta.", new() { ["current"] = "La contraseña actual no es correcta." });
            if (Passwords.Problem(req.New, a.Email) is { } pw) return Fields(pw, new() { ["new"] = pw });
            await accounts.SetPasswordAsync(a.Id, Passwords.Hash(req.New!), ct);
            // Cambiar la contraseña cierra todas las sesiones; esta sigue abierta con una nueva.
            var token = await accounts.CreateSessionAsync(a.Id, ct);
            return Results.Ok(new { token, name = a.Name });
        }).RequireRateLimiting(Strict);

        g.MapPost("/password/forgot", async (ForgotRequest req, AccountRepository accounts, AccountMailer mailer, CancellationToken ct) =>
        {
            var email = NormEmail(req.Email);
            if (!CustomerValidator.IsValidEmail(email)) return Fields("Escribe un correo válido.", new() { ["email"] = "Escribe un correo válido." });
            // Siempre la misma respuesta: no revela si el correo tiene cuenta.
            if (await accounts.GetByEmailAsync(email, ct) is { } a)
                await mailer.SendPasswordResetAsync(a.Email, a.Name, await accounts.CreatePasswordResetAsync(a.Id, ct), ct);
            return Results.Accepted(value: new { ok = true });
        }).RequireRateLimiting(Mail);

        g.MapPost("/password/reset", async (ResetRequest req, AccountRepository accounts, CancellationToken ct) =>
        {
            if (!AccountTokens.LooksValid(req.Token)) return Error("El enlace no es válido. Pide uno nuevo.");
            if (Passwords.Problem(req.Password) is { } pw) return Fields(pw, new() { ["password"] = pw });
            if (await accounts.ConsumePasswordResetAsync(req.Token!, ct) is not { } id)
                return Error("El enlace venció o ya se usó. Pide uno nuevo.");
            await accounts.SetPasswordAsync(id, Passwords.Hash(req.Password!), ct);
            return Results.Ok(new { ok = true });
        }).RequireRateLimiting(Strict);

        g.MapPost("/delete", async (DeleteRequest req, HttpRequest http, AccountRepository accounts, CancellationToken ct) =>
        {
            if (await CurrentAsync(http, accounts, ct) is not { } a) return Results.Unauthorized();
            if (!Passwords.Verify(req.Password ?? "", a.PasswordHash))
                return Fields("La contraseña no es correcta.", new() { ["password"] = "La contraseña no es correcta." });
            await accounts.DeleteAsync(a.Id, ct);
            return Results.NoContent();
        }).RequireRateLimiting(Strict);
    }

    /// <summary>Hasta 5 vehículos con los mismos campos que guarda el navegador; todo lo demás se descarta.</summary>
    public static JsonArray CleanGarage(JsonArray input)
    {
        static string? Str(JsonNode? n, int max) =>
            n is JsonValue v && v.TryGetValue<string>(out var s) && (s = s.Trim()).Length is > 0 && s.Length <= max ? s : null;

        var result = new JsonArray();
        foreach (var node in input.OfType<JsonObject>().Take(5))
        {
            if (Str(node["brand"], 40) is not { } brand) continue;
            int? year = node["year"] is JsonValue y && y.TryGetValue<int>(out var yi) && yi is >= 1950 and <= 2100 ? yi : null;
            result.Add(new JsonObject
            {
                ["id"] = Str(node["id"], 40) ?? Guid.NewGuid().ToString("N")[..12],
                ["brand"] = brand,
                ["model"] = Str(node["model"], 60),
                ["year"] = year,
                ["engine"] = Str(node["engine"], 5),
            });
        }
        return result;
    }
}
