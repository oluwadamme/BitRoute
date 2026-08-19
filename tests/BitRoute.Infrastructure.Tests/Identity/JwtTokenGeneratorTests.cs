using BitRoute.Domain.Enums;
using BitRoute.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace BitRoute.Infrastructure.Tests.Identity;

public class JwtTokenGeneratorTests
{
    private const string SigningKey = "unit-test-signing-key-that-is-long-enough-123";
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static JwtOptions DefaultOptions() => new()
    {
        Issuer = "bitroute",
        Audience = "bitroute-clients",
        SigningKey = SigningKey,
        AccessTokenMinutes = 60,
        RefreshTokenDays = 30,
    };

    private static JwtTokenGenerator CreateGenerator(JwtOptions? options = null) =>
        new(Options.Create(options ?? DefaultOptions()), new FixedClock(Now));

    private static TokenValidationParameters ValidationParameters(string key) => new()
    {
        ValidIssuer = "bitroute",
        ValidAudience = "bitroute-clients",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        RoleClaimType = JwtTokenGenerator.RoleClaimType,
        NameClaimType = JwtRegisteredClaimNames.Sub,
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (_, _, _, _) => true,
    };

    [Fact]
    public async Task GenerateAccessToken_EmbedsSubEmailAndRoleClaims()
    {
        var result = CreateGenerator().GenerateAccessToken(
            UserId, "ada@example.com", [UserRole.Passenger]);

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(result.Token, ValidationParameters(SigningKey));

        Assert.True(validation.IsValid);
        var token = (JsonWebToken)validation.SecurityToken;
        Assert.Equal(UserId.ToString(), token.Subject);
        Assert.Equal("ada@example.com", token.GetClaim(JwtRegisteredClaimNames.Email).Value);
        Assert.Equal("Passenger", token.GetClaim(JwtTokenGenerator.RoleClaimType).Value);
    }

    [Fact]
    public async Task GenerateAccessToken_EmitsOneRoleClaimPerRole()
    {
        var result = CreateGenerator().GenerateAccessToken(
            UserId, "ops@example.com", [UserRole.Operator, UserRole.Admin]);

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(result.Token, ValidationParameters(SigningKey));

        var roles = ((JsonWebToken)validation.SecurityToken).Claims
            .Where(c => c.Type == JwtTokenGenerator.RoleClaimType)
            .Select(c => c.Value)
            .ToArray();
        Assert.Equal(["Operator", "Admin"], roles);
    }

    [Fact]
    public void GenerateAccessToken_ExpiryHonorsConfiguredMinutes()
    {
        var result = CreateGenerator().GenerateAccessToken(
            UserId, "ada@example.com", [UserRole.Passenger]);

        Assert.Equal(Now.AddMinutes(60), result.ExpiresAt);

        var token = new JsonWebTokenHandler().ReadJsonWebToken(result.Token);
        Assert.Equal(Now.AddMinutes(60).UtcDateTime, token.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GenerateAccessToken_FailsValidation_WithWrongKey()
    {
        var result = CreateGenerator().GenerateAccessToken(
            UserId, "ada@example.com", [UserRole.Passenger]);

        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(
            result.Token,
            ValidationParameters("a-completely-different-signing-key-9876543210"));

        Assert.False(validation.IsValid);
    }

    [Fact]
    public async Task GenerateAccessToken_SetsIssuerAndAudience()
    {
        var result = CreateGenerator().GenerateAccessToken(
            UserId, "ada@example.com", [UserRole.Passenger]);

        var validation = await new JsonWebTokenHandler()
            .ValidateTokenAsync(result.Token, ValidationParameters(SigningKey));

        var token = (JsonWebToken)validation.SecurityToken;
        Assert.Equal("bitroute", token.Issuer);
        Assert.Equal("bitroute-clients", Assert.Single(token.Audiences));
    }
}
