namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// The category of an error. The HTTP status comes from the category (ResponseTreatment), so a new domain code
/// already gets the right status — no table to update.
/// Rule of thumb: wrong SHAPE → Validation (400); identity, uniqueness or concurrency → Conflict (409);
/// a business rule on a well-formed request → BusinessRule (422).
/// </summary>
public enum ErrorKind
{
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    BusinessRule,
    Internal
}

/// <summary>A TYPED error: a stable code, a default message and the category that decides the HTTP status.</summary>
public sealed record ErrorCode(string Code, string DefaultMessage, ErrorKind Kind)
{
    public Error ToError(string? field = null, string? message = null) => new()
    {
        Code = Code,
        Message = message ?? DefaultMessage,
        Field = field,
        Kind = Kind
    };

    public override string ToString() => Code;
}
