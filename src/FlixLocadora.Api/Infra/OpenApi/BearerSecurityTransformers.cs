using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.OpenApi;

/// <summary>Declares the JWT bearer scheme, so Scalar shows where to paste the token.</summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Token from POST /api/auth/login (data.accessToken)."
        };

        return Task.CompletedTask;
    }
}

/// <summary>
/// Operations that are not [AllowAnonymous] get the bearer requirement and the 401/403 responses (envelope).
/// While the domain controllers are anonymous, only the account endpoints show the lock — the OpenAPI document
/// itself tells which endpoints are protected.
/// </summary>
public sealed class AuthorizationResponsesTransformer : IOpenApiOperationTransformer
{
    public async Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeName, context.Document)] = []
        });

        var schema = await context.GetOrCreateSchemaAsync(typeof(ApiResponse), null, cancellationToken);
        operation.Responses ??= new OpenApiResponses();
        foreach (var (status, description) in new[] { ("401", "Missing, invalid or expired token (UNAUTHORIZED)."), ("403", "The user's roles do not allow this operation (FORBIDDEN).") })
        {
            operation.Responses.TryAdd(status, new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType> { ["application/json"] = new() { Schema = schema } }
            });
        }
    }
}
