namespace FlixLocadora.Api.Business.Common;

/// <summary>Normalization of user input before it reaches the database (masks, spaces, case).</summary>
public static class Text
{
    /// <summary>Only the digits: "(11) 98765-4321" → "11987654321".</summary>
    public static string Digits(string? value) => new((value ?? "").Where(char.IsAsciiDigit).ToArray());

    /// <summary>Only the digits, or null when nothing is left (optional fields).</summary>
    public static string? DigitsOrNull(string? value) => Digits(value) is { Length: > 0 } digits ? digits : null;

    /// <summary>Trimmed text, or null when empty (optional fields).</summary>
    public static string? TrimmedOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Trimmed and upper-cased: " sp " → "SP".</summary>
    public static string Upper(string? value) => (value ?? "").Trim().ToUpperInvariant();
}
