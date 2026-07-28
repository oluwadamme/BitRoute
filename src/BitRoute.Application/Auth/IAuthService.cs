namespace BitRoute.Application.Auth;

/// <summary>
/// The auth use cases the API exposes. Register and login start a new session;
/// refresh rotates within one; logout revokes one.
/// </summary>
public interface IAuthService
{
    Task<ApiResponse<AuthResult>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<AuthResult>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
