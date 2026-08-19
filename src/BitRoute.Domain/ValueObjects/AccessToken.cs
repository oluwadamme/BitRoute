namespace BitRoute.Domain.ValueObjects;

/// <summary>
/// A signed access token and the instant it expires. The expiry is returned alongside
/// the token so callers can tell clients when to refresh without parsing the JWT.
/// </summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
