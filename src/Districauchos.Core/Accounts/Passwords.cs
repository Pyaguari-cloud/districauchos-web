using System.Security.Cryptography;
using System.Text;

namespace Districauchos.Core.Accounts;

/// <summary>
/// Contraseñas de las cuentas de clientes (pedido del cliente 2026-11-23: usuario = correo + contraseña).
/// Nunca se guarda la contraseña: solo PBKDF2-SHA512 con sal propia (recomendación OWASP: 210 000 vueltas).
/// Formato guardado: <c>pbkdf2-sha512$vueltas$sal$hash</c> (Base64), así se pueden subir las vueltas más adelante.
/// </summary>
public static class Passwords
{
    public const int MinLength = 8;
    public const int MaxLength = 128;
    private const int Iterations = 210_000;
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Scheme = "pbkdf2-sha512";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA512, HashBytes);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme || !int.TryParse(parts[1], out var iterations) || iterations < 10_000) return false;
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException) { return false; }
        var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA512, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Hash falso para comparar cuando el correo no existe: tarda lo mismo y no revela qué correos están registrados.</summary>
    public static readonly string Dummy = Hash("districauchos-dummy-password");

    /// <summary>Reglas simples y claras para el cliente: largo mínimo y no ser una contraseña obvia. Devuelve el error o null.</summary>
    public static string? Problem(string? password, string? email = null)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinLength) return $"La contraseña debe tener al menos {MinLength} caracteres.";
        if (password.Length > MaxLength) return $"La contraseña puede tener hasta {MaxLength} caracteres.";
        if (password.Distinct().Count() < 4) return "La contraseña es muy fácil de adivinar. Usa letras y números distintos.";
        var lower = password.ToLowerInvariant();
        if (Common.Contains(lower) || Common.Any(c => lower.StartsWith(c) && lower.Length <= c.Length + 2))
            return "Esa contraseña es muy común. Elige otra.";
        if (email is { Length: > 0 } && lower.Contains(email.Split('@')[0].ToLowerInvariant()) && email.Split('@')[0].Length >= 4)
            return "La contraseña no puede contener tu correo.";
        return null;
    }

    private static readonly HashSet<string> Common = new(StringComparer.Ordinal)
    {
        "12345678", "123456789", "1234567890", "password", "contraseña", "contrasena", "qwertyui", "qwerty123",
        "11111111", "00000000", "abcd1234", "abc12345", "iloveyou", "districauchos", "ecuador", "quito123",
    };
}

/// <summary>Tokens de sesión y de recuperación: 32 bytes al azar para el cliente; en la base solo su SHA-256.</summary>
public static class AccountTokens
{
    public static string New() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] HashOf(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static bool LooksValid(string? token) => token is { Length: >= 40 and <= 64 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
