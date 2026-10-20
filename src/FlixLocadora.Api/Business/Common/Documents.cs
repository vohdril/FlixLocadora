namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// CNPJ, numeric OR ALPHANUMERIC: since July 2026 the Receita Federal issues CNPJs with letters in the first 12
/// positions (IN RFB 2.229/2024). The 2 check digits stay numeric; modulus 11 with each character worth its
/// ASCII code minus 48 (digits 0–9, A = 17 … Z = 42).
/// </summary>
public static class Cnpj
{
    private static readonly int[] FirstWeights = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>"12.abc.345/01de-35" → "12ABC34501DE35".</summary>
    public static string Normalize(string? value)
        => new((value ?? "").Where(char.IsAsciiLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public static bool IsValid(string? value)
    {
        var cnpj = Normalize(value);
        if (cnpj.Length != 14 || (value ?? "").Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '/' or '-' or ' ')))
            return false;

        if (!cnpj[..12].All(c => char.IsAsciiDigit(c) || char.IsAsciiLetterUpper(c)) || !char.IsAsciiDigit(cnpj[12]) || !char.IsAsciiDigit(cnpj[13]))
            return false;

        return !cnpj.All(c => c == cnpj[0])
            && cnpj[12] - '0' == Documents.Modulus11(cnpj, FirstWeights)
            && cnpj[13] - '0' == Documents.Modulus11(cnpj, SecondWeights);
    }
}

/// <summary>CPF (individual taxpayer number): 11 digits, two modulus-11 check digits.</summary>
public static class Cpf
{
    private static readonly int[] FirstWeights = [10, 9, 8, 7, 6, 5, 4, 3, 2];
    private static readonly int[] SecondWeights = [11, 10, 9, 8, 7, 6, 5, 4, 3, 2];

    /// <summary>"123.456.789-09" → "12345678909".</summary>
    public static string Normalize(string? value) => Text.Digits(value);

    public static bool IsValid(string? value)
    {
        var cpf = Normalize(value);
        if (cpf.Length != 11 || (value ?? "").Any(c => !char.IsAsciiDigit(c) && c is not ('.' or '-' or ' ')))
            return false;

        return !cpf.All(c => c == cpf[0])
            && cpf[9] - '0' == Documents.Modulus11(cpf, FirstWeights)
            && cpf[10] - '0' == Documents.Modulus11(cpf, SecondWeights);
    }
}

internal static class Documents
{
    public static int Modulus11(string value, int[] weights)
    {
        var sum = 0;
        for (var i = 0; i < weights.Length; i++)
            sum += (value[i] - '0') * weights[i];

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
