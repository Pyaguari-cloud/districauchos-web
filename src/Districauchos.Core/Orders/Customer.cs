using System.Text.RegularExpressions;
using Districauchos.Core.Pricing;
using Districauchos.Core.Text;

namespace Districauchos.Core.Orders;

/// <summary>
/// Tipo de documento para la factura (la factura la emite el local en su POS). Decisión del cliente:
/// siempre con datos, sin "consumidor final", sea cual sea el monto.
/// </summary>
public enum DocType { Cedula, Ruc, Pasaporte }

/// <summary>Lo que escribe el comprador en el formulario (sin validar).</summary>
public sealed record CustomerInput(
    string? Name, string? DocType, string? DocNumber, string? Phone, string? Email, string? Address, string? City, string? Notes);

/// <summary>Datos del comprador ya validados y normalizados. Dato personal: nunca se publica.</summary>
public sealed record Customer(
    string Name, DocType DocType, string DocNumber, string Phone, string Email, string? Address, string? City, string? Notes);

public static partial class CustomerValidator
{
    /// <summary>Valida y normaliza. Errores por campo, en español, listos para mostrar.</summary>
    public static (Customer? Customer, Dictionary<string, string> Errors) Validate(
        CustomerInput input, Delivery delivery)
    {
        var errors = new Dictionary<string, string>();

        var name = Clean(input.Name, 100);
        if (name.Length < 3 || !name.Any(char.IsLetter)) errors["name"] = "Escribe tu nombre y apellido.";

        DocType? docType = (input.DocType ?? "").Trim().ToLowerInvariant() switch
        {
            "cedula" => DocType.Cedula,
            "ruc" => DocType.Ruc,
            "pasaporte" => DocType.Pasaporte,
            _ => null,
        };
        var docNumber = "";
        var rawDoc = Clean(input.DocNumber, 20).Replace(" ", "").Replace("-", "");
        switch (docType)
        {
            case DocType.Cedula:
                if (!IsValidCedula(rawDoc)) errors["docNumber"] = "La cédula no es válida (10 dígitos).";
                docNumber = rawDoc;
                break;
            case DocType.Ruc:
                if (!IsValidRuc(rawDoc)) errors["docNumber"] = "El RUC no es válido (13 dígitos).";
                docNumber = rawDoc;
                break;
            case DocType.Pasaporte:
                if (!Passport().IsMatch(rawDoc)) errors["docNumber"] = "Escribe el número de pasaporte.";
                docNumber = rawDoc.ToUpperInvariant();
                break;
            default:
                errors["docType"] = "Elige el tipo de documento para la factura.";
                break;
        }

        var phone = NormalizePhone(input.Phone);
        if (phone is null) errors["phone"] = "Escribe un celular válido, por ejemplo 0987654321.";

        var email = Clean(input.Email, 120).ToLowerInvariant();
        if (!Email().IsMatch(email)) errors["email"] = "Escribe un correo válido.";

        string? address = null, city = null;
        if (delivery is Delivery.Quito or Delivery.National)
        {
            address = Clean(input.Address, 200);
            if (address.Length < 5) errors["address"] = "Escribe la dirección de entrega.";
        }
        if (delivery == Delivery.National)
        {
            city = Clean(input.City, 60);
            if (city.Length < 2) errors["city"] = "Escribe la ciudad de destino.";
        }
        else if (delivery == Delivery.Quito) city = "Quito";

        var notes = Clean(input.Notes, 300);
        if (errors.Count > 0) return (null, errors);
        return (new Customer(name, docType!.Value, docNumber, phone!, email, address, city, notes.Length > 0 ? notes : null), errors);
    }

    /// <summary>Cédula ecuatoriana: provincia 01–24 o 30, tercer dígito &lt; 6 y dígito verificador (módulo 10).</summary>
    public static bool IsValidCedula(string s)
    {
        if (s.Length != 10 || !s.All(char.IsAsciiDigit)) return false;
        var province = int.Parse(s[..2]);
        if (province is < 1 or > 24 && province != 30) return false;
        if (s[2] - '0' >= 6) return false;
        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            var d = (s[i] - '0') * (i % 2 == 0 ? 2 : 1);
            sum += d > 9 ? d - 9 : d;
        }
        var check = (10 - sum % 10) % 10;
        return check == s[9] - '0';
    }

    /// <summary>
    /// RUC: 13 dígitos, provincia válida y establecimiento distinto de 000. Las personas naturales
    /// (tercer dígito &lt; 6) además llevan su cédula al inicio. Las sociedades usan otro algoritmo y
    /// el SRI lo valida al facturar, así que aquí no se rechazan.
    /// </summary>
    public static bool IsValidRuc(string s)
    {
        if (s.Length != 13 || !s.All(char.IsAsciiDigit) || s[10..] == "000") return false;
        var province = int.Parse(s[..2]);
        if (province is < 1 or > 24 && province != 30) return false;
        return s[2] - '0' >= 6 || IsValidCedula(s[..10]);
    }

    /// <summary>Celular a formato internacional (+593…), como lo pide PayPhone.</summary>
    public static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var plus = raw.TrimStart().StartsWith('+');
        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());
        if (!plus && digits.Length == 10 && digits.StartsWith("09")) return "+593" + digits[1..];
        if (digits.StartsWith("5939") && digits.Length == 12) return "+" + digits;
        if (plus && digits.Length is >= 8 and <= 15) return "+" + digits;
        return null;
    }

    private static string Clean(string? s, int max)
    {
        var t = TextNorm.CollapseSpaces((s ?? "").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ')).Trim();
        return t.Length > max ? t[..max].Trim() : t;
    }

    [GeneratedRegex(@"^[^@\s]{1,64}@[^@\s]+\.[a-z]{2,}$", RegexOptions.IgnoreCase)] private static partial Regex Email();
    [GeneratedRegex(@"^[A-Za-z0-9]{5,20}$")] private static partial Regex Passport();
}
