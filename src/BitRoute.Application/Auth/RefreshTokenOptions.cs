namespace BitRoute.Application.Auth;

/// <summary>
/// The slice of token configuration the application layer needs: how long a refresh
/// token lives. Bound from the same "Jwt" section Infrastructure uses, but kept as a
/// separate type so Application never references Infrastructure's options.
/// </summary>
public sealed class RefreshTokenOptions
{
    public const string SectionName = "Jwt";

    public int RefreshTokenDays { get; init; }

    public TimeSpan Lifetime => TimeSpan.FromDays(RefreshTokenDays);
}
