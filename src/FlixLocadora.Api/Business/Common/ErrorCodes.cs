namespace FlixLocadora.Api.Business.Common;

/// <summary>
/// General error catalog. Each feature keeps its own codes next to it (e.g. Business/Customers/Generated/CustomerConstraints.g.cs).
/// Messages are in English (code language); clients translate by <see cref="ErrorCode.Code"/>.
/// </summary>
public static class ErrorCodes
{
    public static readonly ErrorCode ValidationFailed = new("VALIDATION_FAILED", "The request has invalid fields.", ErrorKind.Validation);
    public static readonly ErrorCode InvalidPagination = new("INVALID_PAGINATION", "Page must be >= 1 and pageSize between 1 and 100.", ErrorKind.Validation);
    public static readonly ErrorCode Unauthorized = new("UNAUTHORIZED", "Authentication is required.", ErrorKind.Unauthorized);
    public static readonly ErrorCode Forbidden = new("FORBIDDEN", "Operation not allowed for this user.", ErrorKind.Forbidden);
    public static readonly ErrorCode NotFound = new("NOT_FOUND", "Record not found.", ErrorKind.NotFound);
    public static readonly ErrorCode Conflict = new("CONFLICT", "The request conflicts with a record that already exists.", ErrorKind.Conflict);
    public static readonly ErrorCode ReferenceConflict = new("REFERENCE_CONFLICT", "The record is referenced by other records, or references a record that does not exist.", ErrorKind.Conflict);
    public static readonly ErrorCode ConcurrencyConflict = new("CONCURRENCY_CONFLICT", "The record was changed by someone else. Reload it and try again.", ErrorKind.Conflict);
    public static readonly ErrorCode ConstraintViolated = new("CONSTRAINT_VIOLATED", "The change violates a rule enforced by the database.", ErrorKind.BusinessRule);
    public static readonly ErrorCode InternalError = new("INTERNAL_ERROR", "Internal server error.", ErrorKind.Internal);
}
