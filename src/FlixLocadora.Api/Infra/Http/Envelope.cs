using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.Http;

/// <summary>Stamps the RequestId on every envelope returned by an action (services never deal with it).</summary>
public sealed class EnvelopeResultFilter(IRequestContext context) : IAsyncResultFilter
{
    public Task OnResultExecutionAsync(ResultExecutingContext ctx, ResultExecutionDelegate next)
    {
        if (ctx.Result is ObjectResult { Value: ApiResponse response })
            response.RequestId ??= context.RequestId;

        return next();
    }
}

/// <summary>BINDING errors (malformed JSON, ?page=abc) in the envelope instead of the default ProblemDetails.</summary>
public static class InvalidModelState
{
    public static IActionResult ToEnvelope(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .SelectMany(entry => entry.Value!.Errors.Select(error => ErrorCodes.ValidationFailed.ToError(
                field: FieldPath.ToJson(entry.Key),
                message: SafeMessage(error.ErrorMessage))))
            .ToList();

        return new BadRequestObjectResult(new ApiResponse
        {
            Status = ResponseStatus.Fail,
            Errors = errors,
            RequestId = context.HttpContext.RequestServices.GetRequiredService<IRequestContext>().RequestId
        });
    }

    /// <summary>Parser messages carry .NET type names and byte positions — they are replaced by the default message.</summary>
    private static string? SafeMessage(string message)
        => string.IsNullOrWhiteSpace(message) || message.Contains("Path:", StringComparison.Ordinal) || message.Contains("System.", StringComparison.Ordinal)
            ? null
            : message;
}

/// <summary>Turns validator and model-state paths ("Items[0].UnitPrice", "$.name") into JSON paths ("items[0].unitPrice").</summary>
public static class FieldPath
{
    public static string? ToJson(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "$")
            return null;

        var trimmed = path.StartsWith("$.", StringComparison.Ordinal) ? path[2..] : path;
        return string.Join('.', trimmed.Split('.').Select(segment => segment.Length == 0 ? segment : JsonNamingPolicy.CamelCase.ConvertName(segment)));
    }
}
