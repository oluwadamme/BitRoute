namespace BitRoute.Application.Auth;

/// <summary>Registration always creates a Passenger; no role is accepted from the client.</summary>
public sealed record RegisterRequest(string Email, string Password, string FullName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// What a successful register, login, or refresh returns: the short-lived access token
/// (with its expiry so clients know when to refresh) and the raw refresh token, which
/// exists only in this response and in the client's storage, never on the server.
/// </summary>
public sealed record AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);
