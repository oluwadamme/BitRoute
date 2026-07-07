namespace BitRoute.Domain.Exceptions;

/// <summary>
/// Thrown when a presented refresh token is unknown, expired, or already revoked.
/// The API maps this to 401 Unauthorized so the caller must log in again.
/// </summary>
public sealed class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException()
        : base("The refresh token is invalid or has expired.")
    {
    }
}
