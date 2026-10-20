using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FlixLocadora.Api.Business.Auth;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Api.Infra.Http;

namespace FlixLocadora.Api.Controllers;

/// <summary>Accounts and tokens (ASP.NET Core Identity + JWT).</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(IAccountService accounts) : ControllerBase
{
    /// <summary>Creates an account with the default role (Auth:DefaultRole, Operator).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Register(RegisterRequest request, CancellationToken ct)
        => ResponseTreatment.General(await accounts.RegisterAsync(request, ct), StatusCodes.Status201Created);

    /// <summary>Checks the password (with lockout after 5 failures) and returns the access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Login(LoginRequest request, CancellationToken ct)
        => ResponseTreatment.General(await accounts.LoginAsync(request, ct));

    /// <summary>The user of the token — the first check that a token works.</summary>
    [HttpGet("me")]
    [Authorize(Policy = Policies.Read)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Me(CancellationToken ct)
        => ResponseTreatment.General(await accounts.MeAsync(ct));

    /// <summary>Creates a role (admins only).</summary>
    [HttpPost("roles")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<ActionResult<ApiResponse>> CreateRole(CreateRoleRequest request, CancellationToken ct)
        => ResponseTreatment.General(await accounts.CreateRoleAsync(request, ct), StatusCodes.Status201Created);

    /// <summary>Gives a role to a user (admins only).</summary>
    [HttpPost("users/{email}/roles/{role}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> AddToRole(string email, string role, CancellationToken ct)
        => ResponseTreatment.General(await accounts.AddToRoleAsync(email, role, ct));

    /// <summary>Takes a role from a user (admins only).</summary>
    [HttpDelete("users/{email}/roles/{role}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<ActionResult<ApiResponse<UserResponse>>> RemoveFromRole(string email, string role, CancellationToken ct)
        => ResponseTreatment.General(await accounts.RemoveFromRoleAsync(email, role, ct));
}
