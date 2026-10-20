namespace FlixLocadora.Api.Business.Common;

/// <summary>Builds envelopes. Services return these; only ResponseTreatment turns them into HTTP.</summary>
public static class Responses
{
    public static ApiResponse Success() => new();

    public static ApiResponse<T> Ok<T>(T data) => new() { Data = data };

    public static ApiResponse Fail(ErrorCode code, string? field = null, string? message = null) => new()
    {
        Status = ResponseStatus.Fail,
        Errors = [code.ToError(field, message)]
    };

    public static ApiResponse<T> Fail<T>(ErrorCode code, string? field = null, string? message = null) => new()
    {
        Status = ResponseStatus.Fail,
        Errors = [code.ToError(field, message)]
    };

    /// <summary>Carries the errors of another failed response (e.g. a business-rule hook) into a typed one.</summary>
    public static ApiResponse<T> Fail<T>(ApiResponse failure) => new()
    {
        Status = ResponseStatus.Fail,
        Errors = failure.Errors
    };

    public static ApiResponse NotFound() => Fail(ErrorCodes.NotFound);

    public static ApiResponse<T> NotFound<T>() => Fail<T>(ErrorCodes.NotFound);
}
