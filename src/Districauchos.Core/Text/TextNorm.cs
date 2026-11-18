using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Districauchos.Core.Text;

/// <summary>Reglas de limpieza del brief §6.1 y §6.2.</summary>
public static partial class TextNorm
{
    private static readonly HashSet<string> EmptyTokens =
        new(StringComparer.OrdinalIgnoreCase) { "", "-", "--", "nulo", "null", "n/a", "na", "#n/a" };

    public static bool IsEmpty(string? value) =>
        value is null || EmptyTokens.Contains(value.Trim());

    /// <summary>Devuelve null si la celda se considera vacía; si no, el valor recortado y con espacios simples.</summary>
    public static string? Clean(string? value) => IsEmpty(value) ? null : CollapseSpaces(value!.Trim());

    public static string CollapseSpaces(string value) => MultiSpace().Replace(value, " ");

    public static string StripAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Encabezados: trim, minúsculas, sin tildes, espacios simples.</summary>
    public static string NormalizeHeader(string? header) =>
        header is null ? "" : CollapseSpaces(StripAccents(header).Trim().ToLowerInvariant());

    /// <summary>Código de producto: trim, mayúsculas, sin espacios internos. Conserva ceros a la izquierda.</summary>
    public static string? NormalizeCode(string? code)
    {
        var c = Clean(code);
        return c is null ? null : c.Replace(" ", "").ToUpperInvariant();
    }

    /// <summary>Clave alternativa para empatar códigos numéricos con/sin ceros a la izquierda.</summary>
    public static string? CodeMatchKey(string? codeNorm)
    {
        if (string.IsNullOrEmpty(codeNorm)) return null;
        if (!codeNorm.All(char.IsAsciiDigit)) return codeNorm;
        var trimmed = codeNorm.TrimStart('0');
        return trimmed.Length == 0 ? "0" : trimmed;
    }

    /// <summary>oem_norm = upper(oem) sin espacios, guiones ni puntos.</summary>
    public static string? NormalizeOem(string? oem)
    {
        var c = Clean(oem);
        if (c is null) return null;
        var n = OemStrip().Replace(StripAccents(c).ToUpperInvariant(), "");
        return n.Length == 0 ? null : n;
    }

    /// <summary>
    /// Separa una celda OEM con varios códigos. Siempre separa por '/', ',' y ';'.
    /// Por espacio solo separa si todas las partes tienen al menos 5 caracteres
    /// (evita partir OEMs escritos con espacios, como "9618 0071"). Validar con datos reales.
    /// </summary>
    public static IReadOnlyList<string> SplitOems(string? oemCell)
    {
        var c = Clean(oemCell);
        if (c is null) return [];
        var result = new List<string>();
        foreach (var part in c.Split(['/', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var bySpace = part.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var pieces = bySpace.Length > 1 && bySpace.All(p => p.Length >= 5) ? bySpace : [part];
            foreach (var p in pieces)
            {
                var n = NormalizeOem(p);
                if (n is not null && !result.Contains(n)) result.Add(n);
            }
        }
        return result;
    }

    /// <summary>Parsea "$ 12,50", "12.50", "1.234,56", "1,234.56", "-3".</summary>
    public static decimal? ParseDecimal(string? value)
    {
        var c = Clean(value);
        if (c is null) return null;
        var s = c.Replace("$", "").Replace(" ", "").Replace(" ", "");
        if (s.Length == 0) return null;

        var lastComma = s.LastIndexOf(',');
        var lastDot = s.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            // El último separador es el decimal; el otro es de miles.
            s = lastComma > lastDot ? s.Replace(".", "").Replace(',', '.') : s.Replace(",", "");
        }
        else if (lastComma >= 0)
        {
            s = s.Replace(',', '.');
        }

        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    /// <summary>Texto para coincidencia: mayúsculas, sin tildes, solo letras/dígitos separados por un espacio.</summary>
    public static string NormalizeForMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var s = StripAccents(RepairEncoding(value)).ToUpperInvariant();
        return CollapseSpaces(NonAlnum().Replace(s, " ")).Trim();
    }

    /// <summary>Repara texto mal codificado solo para mostrar: "PU?O" → "PUÑO".</summary>
    public static string RepairEncoding(string value)
    {
        var s = value.Replace('�', '?');
        return QuestionBetweenLetters().Replace(s, m =>
            char.IsLower(m.Value[0]) ? m.Value[0] + "ñ" : m.Value[0] + "Ñ");
    }

    [GeneratedRegex(@"\s{2,}")] private static partial Regex MultiSpace();
    [GeneratedRegex(@"[\s\-\.]")] private static partial Regex OemStrip();
    [GeneratedRegex(@"[^A-Z0-9]+")] private static partial Regex NonAlnum();
    [GeneratedRegex(@"\p{L}\?(?=\p{L})")] private static partial Regex QuestionBetweenLetters();
}
