using BitRoute.Domain.Exceptions;

namespace BitRoute.Domain.Entities;

/// <summary>
/// A refresh token belonging to a login session. Tokens rotate: each refresh consumes
/// the current token and issues a new one in the same session (family). Presenting a
/// token that was already consumed is treated as theft, and the whole family is revoked.
/// Only a hash of the raw token value is stored here, never the raw token itself.
/// All time is passed in as UTC-based <see cref="DateTimeOffset"/> so the rules stay
/// testable and timezone-explicit.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; }
    public Guid SessionId { get; }
    public Guid UserId { get; }
    public string TokenHash { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset ExpiresAt { get; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private RefreshToken(
        Guid id,
        Guid sessionId,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        SessionId = sessionId;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    /// <summary>
    /// Issues the first token of a brand-new login session, allocating a fresh session id.
    /// </summary>
    public static RefreshToken IssueForNewSession(
        Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
        => Issue(Guid.NewGuid(), userId, tokenHash, now, lifetime);

    /// <summary>
    /// Issues the next token in this token's session, keeping the session id so the whole
    /// family can be revoked together if a consumed token is ever replayed. The new expiry
    /// is measured from <paramref name="now"/>, which is how the 30-day window slides.
    /// </summary>
    public RefreshToken IssueNextInSession(string tokenHash, DateTimeOffset now, TimeSpan lifetime)
        => Issue(SessionId, UserId, tokenHash, now, lifetime);

    private static RefreshToken Issue(
        Guid sessionId, Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Lifetime must be positive.");
        }

        return new RefreshToken(Guid.NewGuid(), sessionId, userId, tokenHash, now, now.Add(lifetime));
    }

    /// <summary>
    /// A token is usable only if it has not been consumed, has not been revoked,
    /// and has not expired.
    /// </summary>
    public bool IsActive(DateTimeOffset now)
        => RevokedAt is null && ConsumedAt is null && now < ExpiresAt;

    /// <summary>
    /// Consumes this token as the first half of a rotation. Consuming a token that was
    /// already consumed throws <see cref="RefreshTokenReuseException"/> (the theft signal);
    /// a revoked or expired token throws <see cref="InvalidRefreshTokenException"/>.
    /// </summary>
    public void Consume(DateTimeOffset now)
    {
        if (RevokedAt is not null)
        {
            throw new InvalidRefreshTokenException();
        }

        if (ConsumedAt is not null)
        {
            throw new RefreshTokenReuseException(SessionId);
        }

        if (now >= ExpiresAt)
        {
            throw new InvalidRefreshTokenException();
        }

        ConsumedAt = now;
    }

    /// <summary>
    /// Marks the token revoked. Idempotent, so revoking an already-revoked token
    /// (for example when sweeping a whole family) is a no-op.
    /// </summary>
    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
