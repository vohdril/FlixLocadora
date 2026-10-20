namespace FlixLocadora.Api.Business.Common;

/// <summary>The 27 federative units (UF).</summary>
public static class BrazilianStates
{
    private static readonly HashSet<string> Codes =
    [
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA",
        "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"
    ];

    public static bool IsValid(string? code) => code is not null && Codes.Contains(code.Trim().ToUpperInvariant());
}
