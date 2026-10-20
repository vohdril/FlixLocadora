using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Common;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FlixLocadora.Api.Infra.Errors;

/// <summary>
/// Last resort: an exception that escaped everything (expected failures are envelopes returned by services;
/// database conflicts are translated by DbExceptionHandler). Logs the exception and answers 500 with the
/// envelope (INTERNAL_ERROR + RequestId) — never the exception itself.
/// Deliberate difference from Foundation.API (ProblemDetails for the 500): one format for every response.
/// </summary>
public sealed class UnhandledExceptionHandler(IOptions<HttpJsonOptions> json, ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception on {Method} {Path}", http.Request.Method, http.Request.Path);

        var response = Responses.Fail(ErrorCodes.InternalError);
        response.RequestId = http.RequestServices.GetRequiredService<IRequestContext>().RequestId;

        http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await http.Response.WriteAsJsonAsync(response, json.Value.SerializerOptions, ct);
        return true;
    }
}
