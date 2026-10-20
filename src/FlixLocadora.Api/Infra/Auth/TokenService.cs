using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using FlixLocadora.Api.Business.Auth;
using FlixLocadora.Api.Data.Auth;

namespace FlixLocadora.Api.Infra.Auth;

/// <summary>
/// Issues the access token with JsonWebTokenHandler (the handler the JwtBearer middleware validates with).
/// The base projects used two token libraries (JWT + System.IdentityModel.Tokens.Jwt) and did not validate
/// issuer or audience.
/// </summary>
internal sealed class TokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    public AccessToken Create(AppUser user, IReadOnlyCollection<string> roles)
    {
        var jwt = options.Value;
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var token = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = user.Id,
                [JwtRegisteredClaimNames.Email] = user.Email ?? "",
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N"),
                [ClaimNames.Role] = roles.ToArray(),
            },
            SigningCredentials = new SigningCredentials(SigningKey(jwt), SecurityAlgorithms.HmacSha256)
        });

        return new AccessToken(token, expires);
    }

    public static SymmetricSecurityKey SigningKey(JwtOptions jwt) => new(Encoding.UTF8.GetBytes(jwt.SigningKey));
}

public static class ClaimNames
{
    /// <summary>Short claim names are kept as issued (MapInboundClaims = false): "sub", "email", "role".</summary>
    public const string Role = "role";
}
