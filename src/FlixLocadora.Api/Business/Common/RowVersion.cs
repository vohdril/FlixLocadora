namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// The rowversion of SQL Server as the clients see it: an opaque string ("version"). The GET returns it; a PUT
/// sends it back; if the row changed in between, EF's concurrency check fails and the API answers 409.
/// </summary>
public static class RowVersion
{
    public static string ToVersion(byte[] rowVersion) => Convert.ToBase64String(rowVersion);

    public static byte[] FromVersion(string version) => Convert.FromBase64String(version);

    /// <summary>A SQL Server rowversion is 8 bytes.</summary>
    public static bool IsValid(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return false;

        Span<byte> buffer = stackalloc byte[16];
        return Convert.TryFromBase64String(version, buffer, out var length) && length == 8;
    }
}
