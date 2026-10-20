using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Common;

namespace FlixLocadora.Api.Infra.Auth;

/// <summary>JWT settings (section "Jwt"). Validated at startup: a missing or short key stops the API, not the first login.</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "";

    public string Audience { get; set; } = "";

    /// <summary>HMAC-SHA256 key, at least 32 bytes. Only from user-secrets or Jwt__SigningKey — never a versioned file.</summary>
    public string SigningKey { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 60;

    /// <summary>True when Development generated a per-process key because none was configured.</summary>
    public bool UsesEphemeralKey { get; set; }
}

/// <summary>Configuration level (the third Decco validation level): checked by ValidateOnStart.</summary>
public sealed class JwtOptionsValidator : AValidatorBase<JwtOptions>
{
    public JwtOptionsValidator()
    {
        RuleFor(o => o.Issuer).NotEmpty();
        RuleFor(o => o.Audience).NotEmpty();
        RuleFor(o => o.SigningKey)
            .Must(key => Encoding.UTF8.GetByteCount(key ?? "") >= 32)
            .WithMessage("Jwt:SigningKey must have at least 32 bytes (user-secrets — scripts/dev-secrets.ps1 — or the Jwt__SigningKey environment variable).");
        RuleFor(o => o.AccessTokenMinutes).InclusiveBetween(1, 1440);
    }
}

/// <summary>Development only: without a configured key the API still starts, with a random key valid until it stops.</summary>
public static class DevelopmentSigningKey
{
    private static readonly Lazy<string> Ephemeral = new(() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

    public static void Apply(JwtOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.SigningKey))
            return;

        options.SigningKey = Ephemeral.Value;
        options.UsesEphemeralKey = true;
    }
}

/// <summary>Runs the FluentValidation validator of an options class through ASP.NET's options validation.</summary>
public sealed class FluentValidateOptions<TOptions>(IServiceProvider provider) : IValidateOptions<TOptions>
    where TOptions : class
{
    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        using var scope = provider.CreateScope();
        var result = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>().Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(e => $"{typeof(TOptions).Name}.{e.PropertyName}: {e.ErrorMessage}"));
    }
}
