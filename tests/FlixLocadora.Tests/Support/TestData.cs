namespace FlixLocadora.Tests.Support;

/// <summary>Fictitious data. The check digits are computed here independently of the production code.</summary>
public static class TestData
{
    private static int _sequence = Random.Shared.Next(100_000, 900_000);

    /// <summary>A unique, valid CNPJ — numeric or alphanumeric (format issued since July 2026).</summary>
    public static string NewCnpj(bool alphanumeric = false)
    {
        var number = Interlocked.Increment(ref _sequence);
        var root = alphanumeric ? $"T{(char)('A' + number % 26)}{number:D6}0001" : $"9{number % 10}{number:D6}0001";
        var first = Modulus11(root, [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = Modulus11(root + first, [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]);
        return $"{root}{first}{second}";
    }

    /// <summary>A unique, valid CPF.</summary>
    public static string NewCpf()
    {
        var root = $"{Interlocked.Increment(ref _sequence) % 1_000_000_000:D9}";
        var first = Modulus11(root, [10, 9, 8, 7, 6, 5, 4, 3, 2]);
        var second = Modulus11(root + first, [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]);
        return $"{root}{first}{second}";
    }

    /// <summary>A unique e-mail of the example.com domain (reserved for documentation, never delivered).</summary>
    public static string NewEmail(string prefix = "user") => $"{prefix}.{Interlocked.Increment(ref _sequence)}@tests.example.com";

    // Each character is worth its ASCII code minus 48 (digits 0-9; letters of the alphanumeric CNPJ from 17).
    private static int Modulus11(string value, int[] weights)
    {
        var remainder = value.Select((c, i) => (c - 48) * weights[i]).Sum() % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
