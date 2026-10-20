using Microsoft.AspNetCore.Authorization;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.Auth;

public static class AuthorizationSetup
{
    /// <summary>
    /// Deny by default (Decco reference/13): the FALLBACK policy requires an authenticated user, so an endpoint
    /// without attributes is protected. Opening one is explicit ([AllowAnonymous]). The named policies exist from
    /// the start — [AllowAnonymous] skips them, but ASP.NET still resolves their names on every request.
    /// </summary>
    public static IServiceCollection AddApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthentication();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Read, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(Policies.Write, policy => policy.RequireRole(Roles.Admin, Roles.Operator))
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(Roles.Admin));

        return services;
    }
}
