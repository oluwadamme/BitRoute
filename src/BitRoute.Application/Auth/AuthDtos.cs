namespace BitRoute.Application.Auth;

/// <summary>Registration always creates a Passenger; no role is accepted from the client.</summary>
/// <example>
/// {
///   "email": "ada@example.com",
///   "password": "Password123!",
///   "fullName": "Ada Lovelace"
/// }
/// </example>
public sealed record RegisterRequest(string Email, string Password, string FullName);

/// <summary>Credentials required to login and start a session.</summary>
/// <example>
/// {
///   "email": "ada@example.com",
///   "password": "Password123!"
/// }
/// </example>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Request to rotate active refresh token.</summary>
/// <example>
/// {
///   "refreshToken": "e2a39281-b541-477c-a496-c692881a293b"
/// }
/// </example>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Request to log out and revoke current session.</summary>
/// <example>
/// {
///   "refreshToken": "e2a39281-b541-477c-a496-c692881a293b"
/// }
/// </example>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// What a successful register, login, or refresh returns: the short-lived access token
/// (with its expiry so clients know when to refresh) and the raw refresh token, which
/// exists only in this response and in the client's storage, never on the server.
/// </summary>
/// <example>
/// {
///   "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIzZmRh...",
///   "accessTokenExpiresAt": "2026-07-28T13:00:00.0000000+00:00",
///   "refreshToken": "e2a39281-b541-477c-a496-c692881a293b"
/// }
/// </example>
public sealed record AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);

/// <summary>Request DTO for provisioning an operator user account (admin-only).</summary>
/// <example>
/// {
///   "email": "operator@bitroute.com",
///   "password": "OperatorPassword123!",
///   "fullName": "Jane Operator"
/// }
/// </example>
public sealed record ProvisionOperatorRequest(string Email, string Password, string FullName);
