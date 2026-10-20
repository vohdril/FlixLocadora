using System.Net;
using FlixLocadora.Api.Business.Auth;
using FlixLocadora.Api.Business.Common;
using FlixLocadora.Tests.Support;

namespace FlixLocadora.Tests.Api;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFixture fixture)
{
    [Fact]
    public async Task Register_login_and_me()
    {
        var email = $"user{Guid.NewGuid():N}@tests.local";
        using var anonymous = fixture.CreateClient();

        var registered = await anonymous.PostEnvelopeAsync<UserResponse>("/api/auth/register", new RegisterRequest(email, "Senha!Forte123"));
        var login = await anonymous.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest(email, "Senha!Forte123"));
        using var authenticated = fixture.CreateClient(login.Data.AccessToken);
        var me = await authenticated.GetEnvelopeAsync<UserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.Created, registered.Status);
        Assert.Equal([Roles.Operator], registered.Data.Roles);
        Assert.Equal("Bearer", login.Data.TokenType);
        Assert.True(login.Data.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal(HttpStatusCode.OK, me.Status);
        Assert.Equal(email, me.Data.Email);
        Assert.Equal([Roles.Operator], me.Data.Roles);
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_401()
    {
        var (email, _) = await fixture.NewOperatorAsync();
        using var client = fixture.CreateClient();

        var wrongPassword = await client.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest(email, "Errada!123"));
        var unknownEmail = await client.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest("ninguem@tests.local", "Errada!123"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.Status);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, wrongPassword.Error.Code);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.Status);
        Assert.Equal(AuthErrors.InvalidCredentials.Code, unknownEmail.Error.Code);
    }

    [Fact]
    public async Task The_fifth_failure_locks_the_account_even_for_the_right_password()
    {
        var (email, _) = await fixture.NewOperatorAsync();
        using var client = fixture.CreateClient();

        for (var attempt = 1; attempt <= 4; attempt++)
            Assert.Equal(AuthErrors.InvalidCredentials.Code, (await client.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest(email, "Errada!123"))).Error.Code);

        var fifth = await client.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest(email, "Errada!123"));
        var rightPassword = await client.PostEnvelopeAsync<TokenResponse>("/api/auth/login", new LoginRequest(email, "Senha!Forte123"));

        Assert.Equal(AuthErrors.AccountLocked.Code, fifth.Error.Code);
        Assert.Equal(AuthErrors.AccountLocked.Code, rightPassword.Error.Code);
        Assert.Contains("minute", rightPassword.Error.Message);
    }

    [Fact]
    public async Task Protected_endpoint_without_or_with_a_tampered_token_is_a_401_envelope()
    {
        var (_, token) = await fixture.NewOperatorAsync();
        using var anonymous = fixture.CreateClient();
        using var tampered = fixture.CreateClient(token[..^4] + (token[^4..] == "AAAA" ? "BBBB" : "AAAA"));

        var withoutToken = await anonymous.GetEnvelopeAsync<UserResponse>("/api/auth/me");
        var withTampered = await tampered.GetEnvelopeAsync<UserResponse>("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, withoutToken.Status);
        Assert.Equal(ErrorCodes.Unauthorized.Code, withoutToken.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(withoutToken.Body.RequestId));
        Assert.Equal(HttpStatusCode.Unauthorized, withTampered.Status);
    }

    [Fact]
    public async Task Duplicate_registration_is_409_and_a_weak_password_is_400()
    {
        var (email, _) = await fixture.NewOperatorAsync();
        using var client = fixture.CreateClient();

        var duplicate = await client.PostEnvelopeAsync<UserResponse>("/api/auth/register", new RegisterRequest(email, "Senha!Forte123"));
        var weak = await client.PostEnvelopeAsync<UserResponse>("/api/auth/register", new RegisterRequest($"x{Guid.NewGuid():N}@tests.local", "fraca"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.Status);
        Assert.Equal(AuthErrors.EmailAlreadyRegistered.Code, duplicate.Error.Code);
        Assert.Equal(HttpStatusCode.BadRequest, weak.Status);
        Assert.All(weak.Body.Errors, e => Assert.Equal("password", e.Field));
    }

    [Fact]
    public async Task Role_management_is_for_admins_only()
    {
        var (operatorEmail, operatorToken) = await fixture.NewOperatorAsync();
        using var asOperator = fixture.CreateClient(operatorToken);
        using var asAdmin = fixture.CreateClient(await fixture.LoginAsAdminAsync());
        var role = $"Auditor{Guid.NewGuid():N}"[..16];

        var forbidden = await asOperator.PostEnvelopeAsync<object>("/api/auth/roles", new CreateRoleRequest(role));
        var created = await asAdmin.PostEnvelopeAsync<object>("/api/auth/roles", new CreateRoleRequest(role));
        var assigned = await asAdmin.PostEnvelopeAsync<UserResponse>($"/api/auth/users/{operatorEmail}/roles/{role}");

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.Status);          // the base projects let anyone do this
        Assert.Equal(ErrorCodes.Forbidden.Code, forbidden.Error.Code);
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Contains(role, assigned.Data.Roles);
    }
}
