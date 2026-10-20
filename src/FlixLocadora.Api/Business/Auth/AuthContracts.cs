using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data.Auth;

namespace FlixLocadora.Api.Business.Auth;

/// <param name="Email">Login e-mail.</param>
/// <param name="Password">At least 8 characters with upper case, lower case, digit and symbol (Identity policy).</param>
public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

/// <param name="AccessToken">The JWT — send it as "Authorization: Bearer {accessToken}".</param>
/// <param name="TokenType">Always "Bearer".</param>
/// <param name="ExpiresAt">When the token stops being accepted (UTC).</param>
public sealed record TokenResponse(string AccessToken, string TokenType, DateTimeOffset ExpiresAt);

public sealed record UserResponse(string Id, string Email, IReadOnlyList<string> Roles);

public sealed record CreateRoleRequest(string Name);

/// <summary>A signed access token (implemented in Infra/Auth — the Business layer does not know how it is signed).</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService : IService
{
    AccessToken Create(AppUser user, IReadOnlyCollection<string> roles);
}

/// <summary>Identity settings of the API (section "Auth"). The admin password comes from user-secrets only.</summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    /// <summary>Role given to self-registered users.</summary>
    public string DefaultRole { get; set; } = Roles.Operator;

    /// <summary>Applies the Identity migrations and creates the roles (and the admin) at startup — Development.</summary>
    public bool SeedOnStartup { get; set; }

    public string? AdminEmail { get; set; }

    /// <summary>Never in a versioned file: user-secrets (scripts/dev-secrets.ps1) or Auth__AdminPassword.</summary>
    public string? AdminPassword { get; set; }
}

public static class AuthErrors
{
    public static readonly ErrorCode InvalidCredentials = new("INVALID_CREDENTIALS", "E-mail or password is incorrect.", ErrorKind.Unauthorized);
    public static readonly ErrorCode AccountLocked = new("ACCOUNT_LOCKED", "Too many failed attempts: the account is temporarily locked.", ErrorKind.Unauthorized);
    public static readonly ErrorCode EmailAlreadyRegistered = new("EMAIL_ALREADY_REGISTERED", "This e-mail is already registered.", ErrorKind.Conflict);
    public static readonly ErrorCode RoleAlreadyExists = new("ROLE_ALREADY_EXISTS", "This role already exists.", ErrorKind.Conflict);
    public static readonly ErrorCode UserNotFound = new("USER_NOT_FOUND", "User not found.", ErrorKind.NotFound);
    public static readonly ErrorCode RoleNotFound = new("ROLE_NOT_FOUND", "Role not found.", ErrorKind.NotFound);
}
