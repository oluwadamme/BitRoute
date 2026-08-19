using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

/// <summary>
/// Persistence for refresh tokens and their session families. Consumed tokens are kept
/// until their natural expiry (not deleted), because reuse detection depends on
/// recognizing a replayed consumed token.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Persists a token with a lifetime matching its expiry.</summary>
    Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>Looks up a token by hash; null means unknown or already expired away.</summary>
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically consumes the token as the first half of a rotation and returns its
    /// consumed state. Throws <see cref="Exceptions.InvalidRefreshTokenException"/> for an
    /// unknown, revoked, or expired token and <see cref="Exceptions.RefreshTokenReuseException"/>
    /// when the token was already consumed, including under concurrent replay.
    /// </summary>
    Task<RefreshToken> ConsumeAsync(string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Revokes an entire session family, removing every token in it.</summary>
    Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
