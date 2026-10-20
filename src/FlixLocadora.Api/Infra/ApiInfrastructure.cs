using System.Text.Json.Serialization;
using FlixLocadora.Api.Infra.Auth;
using FlixLocadora.Api.Infra.Errors;
using FlixLocadora.Api.Infra.Filters;
using FlixLocadora.Api.Infra.Http;
using FlixLocadora.Api.Infra.OpenApi;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra;

/// <summary>The web plumbing: controllers, filters, envelope, OpenAPI, errors, CORS, health and authorization.</summary>
public static class ApiInfrastructure
{
    public const string CorsPolicy = "Frontend";

    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Explicit origins only — never SetIsOriginAllowed(_ => true) with credentials.
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(o => o.AddPolicy(CorsPolicy, p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

        services.AddHttpContextAccessor();
        services.AddScoped<IRequestContext, HttpRequestContext>();

        services
            .AddControllers(o =>
            {
                o.Filters.Add<RequestValidationFilter>();
                o.Filters.Add<EnvelopeResultFilter>();
                // One validation technology: FluentValidation. Without this, MVC would also treat every
                // non-nullable reference property as [Required] and answer before the validators run.
                o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
            })
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            // BINDING errors (e.g. ?page=abc, malformed JSON) also come out in the envelope.
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = InvalidModelState.ToEnvelope);
        services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi(o => o
            .AddDocumentTransformer<BearerSecuritySchemeTransformer>()
            .AddOperationTransformer<StandardResponsesTransformer>()
            .AddOperationTransformer<AuthorizationResponsesTransformer>()
            .AddOperationTransformer<CrudSummariesTransformer>());

        // Exception handlers run in registration order: the specific ones first, the catch-all last.
        services.AddProblemDetails();
        services.AddExceptionHandler<DbExceptionHandler>();
        services.AddExceptionHandler<UnhandledExceptionHandler>();

        services.AddHealthChecks();
        services.AddApiAuthorization();

        return services;
    }
}
