using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using FlixLocadora.Api.Controllers;

namespace FlixLocadora.Api.Infra.OpenApi;

/// <summary>
/// The XML comments of the generic CrudController do not reach the operations of the derived controllers
/// (the actions are declared on a closed generic base type), so the CRUD operations get their summary here,
/// named after the resource ("Lists the purchase orders, paged."). Actions declared on the derived controller
/// keep their own XML comments.
/// </summary>
public sealed partial class CrudSummariesTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(operation.Summary)
            || context.Description.ActionDescriptor is not ControllerActionDescriptor action
            || action.MethodInfo.DeclaringType is not { IsGenericType: true } declaring
            || declaring.GetGenericTypeDefinition() != typeof(CrudController<,,,,>))
        {
            return Task.CompletedTask;
        }

        var resource = Words().Replace(action.ControllerName, " $1").Trim().ToLowerInvariant();
        operation.Summary = action.ActionName switch
        {
            "List" => $"Lists the {resource}, paged.",
            "Get" => $"Gets one of the {resource} by id.",
            "Create" => $"Creates one of the {resource}.",
            "Update" => $"Updates one of the {resource} (the whole record).",
            "Delete" => $"Deletes one of the {resource}.",
            _ => operation.Summary
        };

        return Task.CompletedTask;
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex Words();
}
