using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Data.Auth;

namespace FlixLocadora.Api.Business.Auth;

public interface IAccountService : IService
{
    Task<ApiResponse<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);

    Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct);

    Task<ApiResponse<UserResponse>> MeAsync(CancellationToken ct);

    Task<ApiResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct);

    Task<ApiResponse<UserResponse>> AddToRoleAsync(string email, string role, CancellationToken ct);

    Task<ApiResponse<UserResponse>> RemoveFromRoleAsync(string email, string role, CancellationToken ct);
}

/// <summary>
/// Identity is the source of users, password hashes, lockout and roles; the API speaks JWT. What changed from the
/// base projects: no cookie sign-in in an API (CheckPasswordSignInAsync instead of PasswordSignInAsync), no token
/// in a non-HttpOnly cookie, role management restricted to admins (it was open to anyone), no blocking .Result.
/// </summary>
public sealed class AccountService(
    UserManager<AppUser> users,
    SignInManager<AppUser> signIn,
    RoleManager<IdentityRole> roles,
    ITokenService tokens,
    ICurrentUser currentUser,
    IOptions<AuthOptions> options,
    TimeProvider clock) : IAccountService
{
    public async Task<ApiResponse<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new AppUser { UserName = request.Email.Trim(), Email = request.Email.Trim() };
        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return FromIdentity<UserResponse>(created);

        await users.AddToRoleAsync(user, options.Value.DefaultRole);
        return Responses.Ok(await ToResponseAsync(user));
    }

    public async Task<ApiResponse<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Responses.Fail<TokenResponse>(AuthErrors.InvalidCredentials);   // same answer as a wrong password

        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            var end = await users.GetLockoutEndDateAsync(user) ?? clock.GetUtcNow();
            var minutes = Math.Max(1, (int)Math.Ceiling((end - clock.GetUtcNow()).TotalMinutes));
            return Responses.Fail<TokenResponse>(AuthErrors.AccountLocked, message: $"Too many failed attempts: try again in {minutes} minute(s).");
        }

        if (!result.Succeeded)
            return Responses.Fail<TokenResponse>(AuthErrors.InvalidCredentials);

        var token = tokens.Create(user, [.. await users.GetRolesAsync(user)]);
        return Responses.Ok(new TokenResponse(token.Token, "Bearer", token.ExpiresAt));
    }

    public async Task<ApiResponse<UserResponse>> MeAsync(CancellationToken ct)
    {
        var user = currentUser.UserId is { } id ? await users.FindByIdAsync(id) : null;
        return user is null ? Responses.Fail<UserResponse>(AuthErrors.UserNotFound) : Responses.Ok(await ToResponseAsync(user));
    }

    public async Task<ApiResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct)
    {
        if (await roles.RoleExistsAsync(request.Name))
            return Responses.Fail(AuthErrors.RoleAlreadyExists, field: "name");

        var created = await roles.CreateAsync(new IdentityRole(request.Name));
        return created.Succeeded ? Responses.Success() : FromIdentity<object>(created);
    }

    public Task<ApiResponse<UserResponse>> AddToRoleAsync(string email, string role, CancellationToken ct)
        => ChangeRoleAsync(email, role, (user, name) => users.AddToRoleAsync(user, name));

    public Task<ApiResponse<UserResponse>> RemoveFromRoleAsync(string email, string role, CancellationToken ct)
        => ChangeRoleAsync(email, role, (user, name) => users.RemoveFromRoleAsync(user, name));

    private async Task<ApiResponse<UserResponse>> ChangeRoleAsync(string email, string role, Func<AppUser, string, Task<IdentityResult>> change)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return Responses.Fail<UserResponse>(AuthErrors.UserNotFound);

        if (!await roles.RoleExistsAsync(role))
            return Responses.Fail<UserResponse>(AuthErrors.RoleNotFound);

        var result = await change(user, role);
        return result.Succeeded || result.Errors.All(e => e.Code is "UserAlreadyInRole" or "UserNotInRole")
            ? Responses.Ok(await ToResponseAsync(user))
            : FromIdentity<UserResponse>(result);
    }

    private async Task<UserResponse> ToResponseAsync(AppUser user)
        => new(user.Id, user.Email ?? "", [.. await users.GetRolesAsync(user)]);

    /// <summary>Identity errors → envelope: duplicates are 409, the password policy is a 400 on "password".</summary>
    private static ApiResponse<T> FromIdentity<T>(IdentityResult result)
    {
        if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
            return Responses.Fail<T>(AuthErrors.EmailAlreadyRegistered, field: "email");

        return new ApiResponse<T>
        {
            Status = ResponseStatus.Fail,
            Errors = result.Errors.Select(e => ErrorCodes.ValidationFailed.ToError(
                field: e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : e.Code.Contains("Email") ? "email" : null,
                message: e.Description)).ToList()
        };
    }
}
