using Scalar.AspNetCore;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Infra;
using FlixLocadora.Api.Infra.Auth;
using FlixLocadora.Api.Infra.Http;

// ─────────────────────────────────────────────────────────────────────────────
// FlixLocadora.API (.NET 10) — created from the interview-api template.
//
// Pipeline of each request:
//   authentication → authorization (fallback = authenticated user; controllers start [AllowAnonymous])
//   → RequestValidationFilter (explicit FluentValidation) → thin controller → service
//   → repository / unit of work (EF Core, database-first) → ResponseTreatment (envelope + real HTTP status)
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApiInfrastructure(builder.Configuration)
    .AddDataLayer(builder.Configuration)
    .AddIdentityAuth(builder.Environment)
    .AddDomainOptions()
    .RegisterDependencies();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors(ApiInfrastructure.CorsPolicy);
app.UseRequestIdHeader();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    // The fallback policy (authenticated user) also reaches these endpoints: they are opened explicitly.
    app.MapOpenApi().AllowAnonymous();                // /openapi/v1.json (OpenAPI 3.1)
    app.MapScalarApiReference().AllowAnonymous();     // /scalar
    app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.MapControllers();

// Without an endpoint, the fallback policy would answer 401 for any unknown URL; this keeps it a 404 envelope.
app.MapFallback((IRequestContext context) =>
{
    var response = Responses.NotFound();
    response.RequestId = context.RequestId;
    return Results.Json(response, statusCode: StatusCodes.Status404NotFound);
}).AllowAnonymous().ExcludeFromDescription();

app.Run();

/// <summary>Exposed for integration tests (WebApplicationFactory).</summary>
public partial class Program;
