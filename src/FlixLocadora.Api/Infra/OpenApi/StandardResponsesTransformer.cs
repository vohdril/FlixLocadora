using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.OpenApi;

/// <summary>
/// Adds 400/404/500 (with the ApiResponse envelope) to EVERY operation of the document (Foundation.API pattern).
/// Actions declare only their success; errors come from here. A transformer instead of an attribute: an
/// attribute is applied action by action and someone forgets it; the transformer is inescapable by construction.
/// </summary>
public sealed class StandardResponsesTransformer : IOpenApiOperationTransformer
{
    private static readonly (string Status, string Description)[] Standard =
    [
        ("400", "Invalid request — errors[] carries the typed code and the field."),
        ("404", "Record not found (NOT_FOUND)."),
        ("500", "Internal error (INTERNAL_ERROR). Details stay in the log, never in the response."),
    ];

    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var schema = await context.GetOrCreateSchemaAsync(typeof(ApiResponse), null, cancellationToken);
        operation.Responses ??= new OpenApiResponses();

        foreach (var (status, description) in Standard)
        {
            operation.Responses.TryAdd(status, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/json"] = new() { Schema = schema }
                }
            });
        }
    }
}
