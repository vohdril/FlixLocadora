using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.Http;

/// <summary>
/// The ONLY envelope → HTTP status translation (Foundation.API ResponseTreatment). Services do not know HTTP;
/// controllers do not pick status codes. Adapted: the status comes from the error CATEGORY (ErrorKind), so a
/// new domain code never needs a table entry. The envelope is in the body even on errors.
/// </summary>
public static class ResponseTreatment
{
    private static readonly IReadOnlyDictionary<ErrorKind, int> StatusByKind = new Dictionary<ErrorKind, int>
    {
        [ErrorKind.Validation] = StatusCodes.Status400BadRequest,
        [ErrorKind.Unauthorized] = StatusCodes.Status401Unauthorized,
        [ErrorKind.Forbidden] = StatusCodes.Status403Forbidden,
        [ErrorKind.NotFound] = StatusCodes.Status404NotFound,
        [ErrorKind.Conflict] = StatusCodes.Status409Conflict,
        [ErrorKind.BusinessRule] = StatusCodes.Status422UnprocessableEntity,
        [ErrorKind.Internal] = StatusCodes.Status500InternalServerError,
    };

    public static ObjectResult General(ApiResponse response, int successStatus = StatusCodes.Status200OK)
        => new(response) { StatusCode = StatusFor(response, successStatus) };

    /// <summary>Exposed for tests: the whole mapping can be checked without starting the API.</summary>
    public static int StatusFor(ApiResponse response, int successStatus = StatusCodes.Status200OK)
        => response.Status == ResponseStatus.Fail
            ? StatusFor(response.Errors.FirstOrDefault()?.Kind ?? ErrorKind.Validation)
            : successStatus;

    public static int StatusFor(ErrorKind kind)
        => StatusByKind.TryGetValue(kind, out var status) ? status : StatusCodes.Status400BadRequest;
}
