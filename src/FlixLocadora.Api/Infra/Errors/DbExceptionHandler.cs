using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Infra.Http;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FlixLocadora.Api.Infra.Errors;

/// <summary>
/// Translates database refusals into the envelope, in ONE place:
///   • DbUpdateConcurrencyException (rowversion changed)          → 409 CONCURRENCY_CONFLICT;
///   • SQL 2601/2627 (unique index/constraint)                      → the feature's code (catalog) or 409 CONFLICT;
///   • SQL 547 (foreign key or CHECK constraint)                    → the feature's code (catalog) or 409 REFERENCE_CONFLICT.
/// Anything else falls through to UnhandledExceptionHandler (500).
/// </summary>
public sealed partial class DbExceptionHandler(IOptions<HttpJsonOptions> json, ILogger<DbExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var error = Translate(exception, http.RequestServices.GetServices<IConstraintErrorCatalog>());
        if (error is null)
            return false;

        logger.LogWarning("Database rejected the change on {Method} {Path}: {Code}", http.Request.Method, http.Request.Path, error.Code);

        var response = new ApiResponse
        {
            Status = ResponseStatus.Fail,
            Errors = [error],
            RequestId = http.RequestServices.GetRequiredService<IRequestContext>().RequestId
        };

        http.Response.StatusCode = ResponseTreatment.StatusFor(error.Kind);
        await http.Response.WriteAsJsonAsync(response, json.Value.SerializerOptions, ct);
        return true;
    }

    public static Error? Translate(Exception exception, IEnumerable<IConstraintErrorCatalog> catalogs)
    {
        if (exception is DbUpdateConcurrencyException)
            return ErrorCodes.ConcurrencyConflict.ToError();

        // SaveChanges wraps the SqlException in a DbUpdateException; ExecuteUpdate/ExecuteDelete throw it directly.
        var sql = exception as SqlException ?? (exception as DbUpdateException)?.InnerException as SqlException;
        if (sql is null)
            return null;

        if (ConstraintName(sql.Message) is { } constraint)
        {
            var statement = StatementPattern().Match(sql.Message) is { Success: true } match ? match.Groups["statement"].Value : null;
            foreach (var catalog in catalogs)
            {
                if (catalog.TryGet(constraint, statement, out var mapped))
                    return mapped;
            }
        }

        return sql.Number switch
        {
            2601 or 2627 => ErrorCodes.Conflict.ToError(),
            547 when sql.Message.Contains("CHECK constraint", StringComparison.OrdinalIgnoreCase) => ErrorCodes.ConstraintViolated.ToError(),
            547 => ErrorCodes.ReferenceConflict.ToError(),
            _ => null
        };
    }

    // 2627: "...UNIQUE KEY constraint 'UQ_X'..."   2601: "...unique index 'UX_X'..."   547: "...constraint "FK_X"..."
    private static string? ConstraintName(string message)
        => ConstraintPattern().Match(message) is { Success: true } match ? match.Groups["name"].Value : null;

    [GeneratedRegex("""(?:constraint|index) ['"](?<name>[^'"]+)['"]""", RegexOptions.IgnoreCase)]
    private static partial Regex ConstraintPattern();

    // 547: "The DELETE statement conflicted with the REFERENCE constraint ..."
    [GeneratedRegex(@"The (?<statement>INSERT|UPDATE|DELETE|MERGE) statement conflicted", RegexOptions.IgnoreCase)]
    private static partial Regex StatementPattern();
}
