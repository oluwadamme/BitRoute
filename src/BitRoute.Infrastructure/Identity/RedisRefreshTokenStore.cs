using System.Text.Json;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using StackExchange.Redis;

namespace BitRoute.Infrastructure.Identity;

/// <summary>
/// Redis-backed refresh-token store. One JSON document per token keyed by hash, with a
/// TTL matching the token's expiry so Redis handles cleanup. A set per session id tracks
/// the family for revocation. Consumed tokens stay until natural expiry so replays are
/// recognizable, and the consume step is guarded by a SET NX marker so two concurrent
/// presentations of the same token cannot both rotate it (the domain entity owns the
/// rule; the marker closes the check-then-act race, like the booking path's transaction).
/// </summary>
public sealed class RedisRefreshTokenStore : IRefreshTokenStore
{
    private const string TokenKeyPrefix = "refresh-token:";
    private const string ConsumedKeyPrefix = "refresh-consumed:";
    private const string SessionKeyPrefix = "refresh-session:";

    private readonly IConnectionMultiplexer _redis;
    private readonly TimeProvider _clock;

    public RedisRefreshTokenStore(IConnectionMultiplexer redis, TimeProvider clock)
    {
        _redis = redis;
        _clock = clock;
    }

    public async Task SaveAsync(RefreshToken token, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var timeToLive = token.ExpiresAt - _clock.GetUtcNow();
        if (timeToLive <= TimeSpan.Zero)
        {
            return;
        }

        var sessionKey = SessionKey(token.SessionId);
        await db.StringSetAsync(TokenKey(token.TokenHash), Serialize(token), timeToLive);
        await db.SetAddAsync(sessionKey, token.TokenHash);
        // The newest token always has the farthest expiry, so push the family's
        // lifetime out with it (this is the sliding window at the family level).
        await db.KeyExpireAsync(sessionKey, timeToLive);
    }

    public async Task<RefreshToken?> FindByHashAsync(
        string tokenHash, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(TokenKey(tokenHash));
        return value.IsNullOrEmpty ? null : Deserialize(value!);
    }

    public async Task<RefreshToken> ConsumeAsync(
        string tokenHash, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var token = await FindByHashAsync(tokenHash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        // The domain rule: throws on revoked/expired (invalid) or stored consumed state (reuse).
        token.Consume(now);

        // Close the concurrent window: only the first of two simultaneous presenters
        // gets to create the marker; the loser sees a replay.
        var remaining = token.ExpiresAt - now;
        var isFirstConsumer = await db.StringSetAsync(
            ConsumedKey(tokenHash), now.ToString("O"), remaining, When.NotExists);
        if (!isFirstConsumer)
        {
            throw new RefreshTokenReuseException(token.SessionId);
        }

        await db.StringSetAsync(TokenKey(tokenHash), Serialize(token), expiry: null, keepTtl: true);
        return token;
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var sessionKey = SessionKey(sessionId);
        var hashes = await db.SetMembersAsync(sessionKey);

        var keys = hashes
            .SelectMany(hash => new RedisKey[] { TokenKey(hash!), ConsumedKey(hash!) })
            .Append(sessionKey)
            .ToArray();
        await db.KeyDeleteAsync(keys);
    }

    private static RedisKey TokenKey(string hash) => TokenKeyPrefix + hash;
    private static RedisKey ConsumedKey(string hash) => ConsumedKeyPrefix + hash;
    private static RedisKey SessionKey(Guid sessionId) => SessionKeyPrefix + sessionId;

    private static RedisValue Serialize(RefreshToken token) =>
        JsonSerializer.Serialize(new RefreshTokenDocument(
            token.Id, token.SessionId, token.UserId, token.TokenHash,
            token.CreatedAt, token.ExpiresAt, token.ConsumedAt, token.RevokedAt));

    private static RefreshToken Deserialize(string json)
    {
        var doc = JsonSerializer.Deserialize<RefreshTokenDocument>(json)!;
        return RefreshToken.Rehydrate(
            doc.Id, doc.SessionId, doc.UserId, doc.TokenHash,
            doc.CreatedAt, doc.ExpiresAt, doc.ConsumedAt, doc.RevokedAt);
    }

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
