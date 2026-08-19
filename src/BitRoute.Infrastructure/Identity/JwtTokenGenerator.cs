using System.Security.Claims;
using System.Text;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Issues HMAC-SHA256-signed JWT access tokens. Claims use the compact JWT names
/// ("sub", "email", "role"); the bearer validation in the Api must set
/// RoleClaimType = "role" and NameClaimType = "sub" to match. Time comes from
/// <see cref="TimeProvider"/> so tests can pin the clock.
/// </summary>
public sealed class JwtTokenGenerator : ITokenGenerator
{
    public const string RoleClaimType = "role";

    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _credentials;

    public JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken GenerateAccessToken(Guid userId, string email, IReadOnlyCollection<UserRole> roles)
    {
        var now = _clock.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(role => new Claim(RoleClaimType, role.ToString())));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }
}
