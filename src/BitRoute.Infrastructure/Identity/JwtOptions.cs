namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Token settings bound from the "Jwt" configuration section (Jwt__* environment
/// variables or the .env file locally). Validated at startup, so a missing or weak
/// signing key stops the app at boot instead of surfacing at the first login.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; }
    public int RefreshTokenDays { get; init; }
}
