using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using FlixLocadora.Api.Business.Auth;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data;
using FlixLocadora.Api.Data.Auth;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace FlixLocadora.Api.Infra.Auth;

public static class IdentityAuth
{
    /// <summary>
    /// Identity Core (users, password hashing, lockout, roles) + JWT bearer. No cookies, one scheme.
    /// The policies and the fallback (authenticated user) are in AuthorizationSetup.
    /// </summary>
    public static IServiceCollection AddIdentityAuth(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.Section);
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.Section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, FluentValidateOptions<JwtOptions>>();
        if (environment.IsDevelopment())
            services.PostConfigure<JwtOptions>(DevelopmentSigningKey.Apply);

        services.AddDbContext<AuthDbContext>((provider, options) => options.UseSqlServer(
            provider.GetRequiredService<IConfiguration>().GetConnectionString(DataLayer.ConnectionName),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AuthDbContext.Schema).EnableRetryOnFailure()));

        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()          // CheckPasswordSignInAsync: password + lockout, without issuing a cookie
            .AddDefaultTokenProviders();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearer>();

        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddHostedService<IdentitySeeder>();

        return services;
    }
}

/// <summary>Turns on everything the base projects turned off: issuer, audience, lifetime and key are all validated.</summary>
internal sealed class ConfigureJwtBearer(IOptions<JwtOptions> jwt) : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme)
            return;

        var settings = jwt.Value;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = settings.Issuer,
            ValidAudience = settings.Audience,
            IssuerSigningKey = TokenService.SigningKey(settings),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Email,
            RoleClaimType = ClaimNames.Role
        };

        // 401 and 403 also answer the envelope: one format for every response.
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                return AuthEnvelope.WriteAsync(context.HttpContext, ErrorCodes.Unauthorized, StatusCodes.Status401Unauthorized);
            },
            OnForbidden = context => AuthEnvelope.WriteAsync(context.HttpContext, ErrorCodes.Forbidden, StatusCodes.Status403Forbidden)
        };
    }

    public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
}

internal static class AuthEnvelope
{
    public static Task WriteAsync(HttpContext http, ErrorCode code, int status)
    {
        var response = Responses.Fail(code);
        response.RequestId = http.RequestServices.GetRequiredService<IRequestContext>().RequestId;

        http.Response.StatusCode = status;
        var json = http.RequestServices.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        return http.Response.WriteAsJsonAsync(response, json);
    }
}

internal sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? User => accessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue(JwtRegisteredClaimNames.Sub);

    public string? Email => User?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => User?.IsInRole(role) == true;
}

/// <summary>
/// Startup (when Auth:SeedOnStartup — Development): applies the Identity migrations, creates the roles and, if
/// Auth:AdminEmail and Auth:AdminPassword are configured (user-secrets), the admin user.
/// </summary>
internal sealed class IdentitySeeder(IServiceProvider provider, IOptions<AuthOptions> auth, IOptions<JwtOptions> jwt, ILogger<IdentitySeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (jwt.Value.UsesEphemeralKey)
            logger.LogWarning("Jwt:SigningKey is not configured: using an ephemeral key (tokens stop working on restart). Run scripts/dev-secrets.ps1.");

        var settings = auth.Value;
        if (!settings.SeedOnStartup)
            return;

        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AuthDbContext>().Database.MigrateAsync(ct);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));
        }

        if (string.IsNullOrWhiteSpace(settings.AdminEmail) || string.IsNullOrWhiteSpace(settings.AdminPassword))
        {
            logger.LogInformation("Admin user not seeded (Auth:AdminEmail / Auth:AdminPassword not configured).");
            return;
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var admin = await users.FindByEmailAsync(settings.AdminEmail);
        if (admin is null)
        {
            admin = new AppUser { UserName = settings.AdminEmail, Email = settings.AdminEmail, EmailConfirmed = true };
            var created = await users.CreateAsync(admin, settings.AdminPassword);
            if (!created.Succeeded)
            {
                logger.LogError("Admin user not created: {Errors}", string.Join("; ", created.Errors.Select(e => e.Description)));
                return;
            }
        }

        if (!await users.IsInRoleAsync(admin, Roles.Admin))
            await users.AddToRoleAsync(admin, Roles.Admin);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
