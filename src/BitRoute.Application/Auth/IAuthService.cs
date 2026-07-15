namespace BitRoute.Application.Auth;

/// <summary>
/// The auth use cases the API exposes. Register and login start a new session;
/// refresh rotates within one; logout revokes one.
/// </summary>
public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
