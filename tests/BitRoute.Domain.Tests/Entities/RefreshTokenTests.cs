using BitRoute.Domain.Entities;
using BitRoute.Domain.Exceptions;

namespace BitRoute.Domain.Tests.Entities;

public class RefreshTokenTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private static RefreshToken NewSessionToken() =>
        RefreshToken.IssueForNewSession(UserId, "hash-1", Now, Lifetime);

    [Fact]
    public void IssueForNewSession_StartsActiveWithFreshSession()
    {
        var token = NewSessionToken();

        Assert.True(token.IsActive(Now));
        Assert.Equal(UserId, token.UserId);
        Assert.Equal(Now.Add(Lifetime), token.ExpiresAt);
        Assert.Null(token.ConsumedAt);
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public void IsActive_ReturnsFalse_WhenExpired()
    {
        var token = NewSessionToken();

        Assert.False(token.IsActive(Now.Add(Lifetime)));
        Assert.False(token.IsActive(Now.Add(Lifetime + TimeSpan.FromSeconds(1))));
    }

    [Fact]
    public void IssueNextInSession_KeepsSessionButChangesTokenIdentity()
    {
        var first = NewSessionToken();

        var second = first.IssueNextInSession("hash-2", Now.AddMinutes(30), Lifetime);

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(first.UserId, second.UserId);
    }

    [Fact]
    public void IssueNextInSession_SlidesTheExpiryWindow()
    {
        var first = NewSessionToken();
        var later = Now.AddDays(10);

        var second = first.IssueNextInSession("hash-2", later, Lifetime);

        Assert.Equal(later.Add(Lifetime), second.ExpiresAt);
    }

    [Fact]
    public void Consume_MarksTokenConsumedAndInactive()
    {
        var token = NewSessionToken();

        token.Consume(Now.AddMinutes(30));

        Assert.NotNull(token.ConsumedAt);
        Assert.False(token.IsActive(Now.AddMinutes(30)));
    }

    [Fact]
    public void Consume_Throws_ReuseException_WhenAlreadyConsumed()
    {
        var token = NewSessionToken();
        token.Consume(Now.AddMinutes(30));

        var ex = Assert.Throws<RefreshTokenReuseException>(() => token.Consume(Now.AddMinutes(31)));
        Assert.Equal(token.SessionId, ex.SessionId);
    }

    [Fact]
    public void Consume_Throws_InvalidException_WhenRevoked()
    {
        var token = NewSessionToken();
        token.Revoke(Now.AddMinutes(5));

        Assert.Throws<InvalidRefreshTokenException>(() => token.Consume(Now.AddMinutes(30)));
    }

    [Fact]
    public void Consume_Throws_InvalidException_WhenExpired()
    {
        var token = NewSessionToken();

        Assert.Throws<InvalidRefreshTokenException>(() => token.Consume(Now.Add(Lifetime)));
    }

    [Fact]
    public void Revoke_IsIdempotent()
    {
        var token = NewSessionToken();

        token.Revoke(Now.AddMinutes(5));
        var firstRevokedAt = token.RevokedAt;
        token.Revoke(Now.AddMinutes(10));

        Assert.Equal(firstRevokedAt, token.RevokedAt);
    }

    [Fact]
    public void Issue_Throws_WhenLifetimeIsNotPositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RefreshToken.IssueForNewSession(UserId, "hash", Now, TimeSpan.Zero));
    }

    [Fact]
    public void Issue_Throws_WhenTokenHashIsMissing()
    {
        Assert.Throws<ArgumentException>(
            () => RefreshToken.IssueForNewSession(UserId, " ", Now, Lifetime));
    }
}
