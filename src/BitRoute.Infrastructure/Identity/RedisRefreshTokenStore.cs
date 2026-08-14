using BitRoute.Domain.Entities;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using BitRoute.Infrastructure.Caching;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Refresh-token store backed by <see cref="ICacheService"/>.
/// One JSON document per token keyed by hash, with a TTL matching the token's expiry.
/// A set per session ID tracks the family for revocation. Consumed tokens stay until
/// natural expiry so replays are recognizable, and the consume step is guarded by an
/// atomic SET NX marker so two concurrent presentations of the same token cannot both rotate it.
/// </summary>
public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    private const string TokenKeyPrefix = "refresh-token:";
    private const string ConsumedKeyPrefix = "refresh-consumed:";
    private const string SessionKeyPrefix = "refresh-session:";

    private readonly ICacheService _cache;
    private readonly TimeProvider _clock;

    public RedisRefreshTokenStore(ICacheService cache, TimeProvider clock)
    {
        _cache = cache;
        _clock = clock;
    }

    public async Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        var timeToLive = token.ExpiresAt - _clock.GetUtcNow();
        if (timeToLive <= TimeSpan.Zero)
        {
            return;
        }

        var sessionKey = SessionKey(token.SessionId);
        var doc = MapToDocument(token);

        await _cache.SetAsync(TokenKey(token.TokenHash), doc, timeToLive, cancellationToken);
        await _cache.SetAddAsync(sessionKey, token.TokenHash, timeToLive, cancellationToken);
    }

    public async Task<RefreshToken?> FindByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default)
    {
        var doc = await _cache.GetAsync<RefreshTokenDocument>(TokenKey(tokenHash), cancellationToken);
        return doc is null ? null : MapFromDocument(doc);
    }

    public async Task<RefreshToken> ConsumeAsync(
        string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var token = await FindByHashAsync(tokenHash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        // Domain rule: throws on revoked/expired (invalid) or stored consumed state (reuse).
        token.Consume(now);

        // Close the concurrent window: only the first of two simultaneous presenters
        // gets to create the marker; the loser sees a replay.
        var remaining = token.ExpiresAt - now;
        var isFirstConsumer = await _cache.SetIfNotExistsAsync(
            ConsumedKey(tokenHash), now.ToString("O"), remaining, cancellationToken);

        if (!isFirstConsumer)
        {
            throw new RefreshTokenReuseException(token.SessionId);
        }

        var updatedDoc = MapToDocument(token);
        await _cache.SetKeepTtlAsync(TokenKey(tokenHash), updatedDoc, cancellationToken);
        return token;
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var sessionKey = SessionKey(sessionId);
        var hashes = await _cache.GetSetMembersAsync(sessionKey, cancellationToken);

        var keys = hashes
            .SelectMany(hash => new[] { TokenKey(hash), ConsumedKey(hash) })
            .Append(sessionKey);

        await _cache.RemoveKeysAsync(keys, cancellationToken);
    }

    private static string TokenKey(string hash) => TokenKeyPrefix + hash;
    private static string ConsumedKey(string hash) => ConsumedKeyPrefix + hash;
    private static string SessionKey(Guid sessionId) => SessionKeyPrefix + sessionId;

    private static RefreshTokenDocument MapToDocument(RefreshToken token) =>
        new(token.Id, token.SessionId, token.UserId, token.TokenHash,
            token.CreatedAt, token.ExpiresAt, token.ConsumedAt, token.RevokedAt);

    private static RefreshToken MapFromDocument(RefreshTokenDocument doc) =>
        RefreshToken.Rehydrate(
            doc.Id, doc.SessionId, doc.UserId, doc.TokenHash,
            doc.CreatedAt, doc.ExpiresAt, doc.ConsumedAt, doc.RevokedAt);

    private sealed record RefreshTokenDocument(
        Guid Id,
        Guid SessionId,
        Guid UserId,
        string TokenHash,
        DateTimeOffset CreatedAt,
        DateTimeOffset ExpiresAt,
        DateTimeOffset? ConsumedAt,
        DateTimeOffset? RevokedAt);
}
