using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.Http;

/// <summary>
/// RequestId of the current request: the X-Request-Id sent by the client, or ASP.NET's TraceIdentifier.
/// Scoped — valid for the whole request (Foundation.API pattern).
/// </summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public const string Header = "X-Request-Id";

    public string RequestId { get; } = Resolve(accessor.HttpContext);

    private static string Resolve(HttpContext? http)
    {
        var received = http?.Request.Headers[Header].ToString();
        return !string.IsNullOrWhiteSpace(received) && received.Length <= 64
            ? received
            : http?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
    }
}

public static class RequestIdMiddleware
{
    /// <summary>Echoes the correlation id in every response, so a client can quote it when reporting a problem.</summary>
    public static IApplicationBuilder UseRequestIdHeader(this IApplicationBuilder app) => app.Use(async (http, next) =>
    {
        var context = http.RequestServices.GetRequiredService<IRequestContext>();
        http.Response.Headers[HttpRequestContext.Header] = context.RequestId;
        await next(http);
    });
}
