using BitRoute.Domain.Entities;
using BitRoute.Domain.Exceptions;
using BitRoute.Infrastructure.Caching;
using BitRoute.Infrastructure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace BitRoute.Infrastructure.Tests.Identity;

public sealed class RedisRefreshTokenStoreTests : IAsyncLifetime
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();
    private IConnectionMultiplexer _redis = null!;
    private RedisRefreshTokenStore _store = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _redis = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        _store = new RedisRefreshTokenStore(new RedisCacheService(_redis, NullLogger<RedisCacheService>.Instance), TimeProvider.System);
    }

    public async Task DisposeAsync()
    {
        _redis.Dispose();
        await _container.DisposeAsync();
    }

    private static RefreshToken NewToken(string hash, DateTimeOffset? now = null) =>
        RefreshToken.IssueForNewSession(
            Guid.NewGuid(), hash, now ?? DateTimeOffset.UtcNow, Lifetime);

    [Fact]
    public async Task Save_ThenFind_RoundTripsAllFields()
    {
        var token = NewToken("hash-roundtrip");
        await _store.SaveAsync(token);

        var found = await _store.FindByHashAsync("hash-roundtrip");

        Assert.NotNull(found);
        Assert.Equal(token.Id, found.Id);
        Assert.Equal(token.SessionId, found.SessionId);
        Assert.Equal(token.UserId, found.UserId);
        Assert.Equal(token.ExpiresAt, found.ExpiresAt);
        Assert.Null(found.ConsumedAt);
        Assert.Null(found.RevokedAt);
    }

    [Fact]
    public async Task Find_ReturnsNull_ForUnknownHash()
    {
        Assert.Null(await _store.FindByHashAsync("hash-never-saved"));
    }

    [Fact]
    public async Task Save_AppliesTtlMatchingTokenLifetime()
    {
        await _store.SaveAsync(NewToken("hash-ttl"));

        var ttl = await _redis.GetDatabase().KeyTimeToLiveAsync("refresh-token:hash-ttl");

        Assert.NotNull(ttl);
        Assert.InRange(ttl.Value, Lifetime - TimeSpan.FromMinutes(1), Lifetime);
    }

    [Fact]
    public async Task Consume_PersistsConsumedState()
    {
        await _store.SaveAsync(NewToken("hash-consume"));

        var consumed = await _store.ConsumeAsync("hash-consume", DateTimeOffset.UtcNow);

        Assert.NotNull(consumed.ConsumedAt);
        var reloaded = await _store.FindByHashAsync("hash-consume");
        Assert.NotNull(reloaded!.ConsumedAt);
    }

    [Fact]
    public async Task Consume_Throws_Invalid_ForUnknownToken()
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => _store.ConsumeAsync("hash-unknown", DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task SecondConsume_Throws_Reuse_WithTheSessionId()
    {
        var token = NewToken("hash-replay");
        await _store.SaveAsync(token);
        await _store.ConsumeAsync("hash-replay", DateTimeOffset.UtcNow);

        var ex = await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => _store.ConsumeAsync("hash-replay", DateTimeOffset.UtcNow));

        Assert.Equal(token.SessionId, ex.SessionId);
    }

    [Fact]
    public async Task Consume_UnderParallelReplay_SucceedsExactlyOnce()
    {
        await _store.SaveAsync(NewToken("hash-race"));

        var attempts = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await _store.ConsumeAsync("hash-race", DateTimeOffset.UtcNow);
                return true;
            }
            catch (RefreshTokenReuseException)
            {
                return false;
            }
        }));

        Assert.Equal(1, attempts.Count(success => success));
    }

    [Fact]
    public async Task RevokeSession_RemovesEveryTokenInTheFamily()
    {
        var first = NewToken("hash-family-1");
        var second = first.IssueNextInSession("hash-family-2", DateTimeOffset.UtcNow, Lifetime);
        await _store.SaveAsync(first);
        await _store.SaveAsync(second);

        await _store.RevokeSessionAsync(first.SessionId);

        Assert.Null(await _store.FindByHashAsync("hash-family-1"));
        Assert.Null(await _store.FindByHashAsync("hash-family-2"));
    }

    [Fact]
    public async Task RevokeSession_DoesNotTouchOtherSessions()
    {
        var victim = NewToken("hash-victim");
        var bystander = NewToken("hash-bystander");
        await _store.SaveAsync(victim);
        await _store.SaveAsync(bystander);

        await _store.RevokeSessionAsync(victim.SessionId);

        Assert.NotNull(await _store.FindByHashAsync("hash-bystander"));
    }
}
