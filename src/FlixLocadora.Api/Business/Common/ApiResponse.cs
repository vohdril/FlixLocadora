using System.ComponentModel;
using System.Text.Json.Serialization;

namespace FlixLocadora.Api.Business.Common;

/// <summary>Response status. Serialized as TEXT ("Fail"), not as a number.</summary>
public enum ResponseStatus
{
    Success,
    PartialSuccess,
    Fail
}

/// <summary>An error of the envelope. The field names the failing input — without it a UI cannot highlight it.</summary>
public sealed class Error
{
    [Description("Stable error code (e.g. NOT_FOUND, CUSTOMER_EMAIL_ALREADY_EXISTS). Clients branch on it, never on the message.")]
    public required string Code { get; init; }

    [Description("Human-readable message. Never carries internal details (exception, SQL, stack trace).")]
    public required string Message { get; init; }

    [Description("Field that caused the error, as a JSON path (e.g. items[0].quantity), when applicable.")]
    public string? Field { get; init; }

    /// <summary>Category that decides the HTTP status (see ResponseTreatment). Not part of the JSON contract.</summary>
    [JsonIgnore]
    public ErrorKind Kind { get; init; } = ErrorKind.Validation;
}

/// <summary>
/// The envelope — the body of EVERY response, success or error (Foundation.API pattern).
/// The HTTP status and the envelope status always agree (see ResponseTreatment): the client never has to
/// choose between the two. The base projects returned HTTP 200 even for errors.
/// </summary>
public class ApiResponse
{
    public ResponseStatus Status { get; init; } = ResponseStatus.Success;

    public IReadOnlyList<Error> Errors { get; init; } = [];

    [Description("Request correlation id (X-Request-Id). Quote it when reporting a problem: it is in the server log.")]
    public string? RequestId { get; set; }
}

public sealed class ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}
