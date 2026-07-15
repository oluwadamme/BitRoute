using BitRoute.Application.Auth;
using BitRoute.Application.Common;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BitRoute.Application.Tests.Auth;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    private readonly FakeIdentityService _identity = new();
    private readonly FakeRefreshTokenStore _store = new();

    private AuthService CreateService() => new(
        _identity,
        _store,
        new FakeCrypto(),
        new FakeTokenGenerator(),
        new FixedClock(Now),
        Options.Create(new RefreshTokenOptions { RefreshTokenDays = 30 }),
        // The production resolver over a real container, so a missing validator
        // registration would fail these tests too.
        new RequestValidator(new ServiceCollection()
            .AddValidatorsFromAssemblyContaining<RegisterRequestValidator>()
            .BuildServiceProvider()));

    [Fact]
    public async Task Register_AlwaysCreatesPassenger_NeverAnotherRole()
    {
        await CreateService().RegisterAsync(
            new RegisterRequest("ada@example.com", "password-8", "Ada Lovelace"));

        Assert.Equal(UserRole.Passenger, _identity.LastCreatedRole);
        Assert.Equal("Ada Lovelace", _identity.LastCreatedFullName);
    }

    [Fact]
    public async Task Register_ReturnsTokens_AndStoresNewSessionToken()
    {
        var result = await CreateService().RegisterAsync(
            new RegisterRequest("ada@example.com", "password-8", "Ada Lovelace"));

        var stored = Assert.Single(_store.Saved);
        Assert.Equal(UserId, stored.UserId);
        Assert.Equal(FakeCrypto.HashOf(result.RefreshToken), stored.TokenHash);
        Assert.Equal(Now.AddDays(30), stored.ExpiresAt);
        Assert.Equal(Now.AddMinutes(60), result.AccessTokenExpiresAt);
        Assert.NotEmpty(result.AccessToken);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_Throws_WithoutTouchingIdentity()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => CreateService().RegisterAsync(new RegisterRequest("not-an-email", "password-8", "Ada Lovelace")));

        Assert.Null(_identity.LastCreatedRole);
    }

    [Fact]
    public async Task Login_WithBadCredentials_PropagatesInvalidCredentials()
    {
        _identity.FailCredentials = true;

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => CreateService().LoginAsync(new LoginRequest("ada@example.com", "wrong")));
        Assert.Empty(_store.Saved);
    }

    [Fact]
    public async Task Refresh_RotatesWithinTheSameSession()
    {
        var service = CreateService();
        var login = await service.LoginAsync(new LoginRequest("ada@example.com", "password-8"));
        var firstSession = _store.Saved[0].SessionId;

        var refreshed = await service.RefreshAsync(new RefreshRequest(login.RefreshToken));

        Assert.Equal(2, _store.Saved.Count);
        Assert.Equal(firstSession, _store.Saved[1].SessionId);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        Assert.NotNull(_store.Saved[0].ConsumedAt);
    }

    [Fact]
    public async Task Refresh_OnReuse_RevokesTheFamily_AndRethrows()
    {
        var service = CreateService();
        var login = await service.LoginAsync(new LoginRequest("ada@example.com", "password-8"));
        await service.RefreshAsync(new RefreshRequest(login.RefreshToken));

        await Assert.ThrowsAsync<RefreshTokenReuseException>(
            () => service.RefreshAsync(new RefreshRequest(login.RefreshToken)));

        Assert.Equal([_store.Saved[0].SessionId], _store.RevokedSessions);
    }

    [Fact]
    public async Task Refresh_WhenUserNoLongerExists_ThrowsInvalidRefreshToken()
    {
        var service = CreateService();
        var login = await service.LoginAsync(new LoginRequest("ada@example.com", "password-8"));
        _identity.UserVanished = true;

        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => service.RefreshAsync(new RefreshRequest(login.RefreshToken)));
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_ThrowsInvalidRefreshToken()
    {
        await Assert.ThrowsAsync<InvalidRefreshTokenException>(
            () => CreateService().RefreshAsync(new RefreshRequest("never-issued")));
    }

    [Fact]
    public async Task Logout_RevokesTheWholeSession()
    {
        var service = CreateService();
        var login = await service.LoginAsync(new LoginRequest("ada@example.com", "password-8"));

        await service.LogoutAsync(new LogoutRequest(login.RefreshToken));

        Assert.Equal([_store.Saved[0].SessionId], _store.RevokedSessions);
    }

    [Fact]
    public async Task Logout_WithUnknownToken_IsANoOp()
    {
        await CreateService().LogoutAsync(new LogoutRequest("never-issued"));

        Assert.Empty(_store.RevokedSessions);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public UserRole? LastCreatedRole;
        public string? LastCreatedFullName;
        public bool FailCredentials;
        public bool UserVanished;

        public Task<AuthUser> CreateUserAsync(
            string email, string password, string fullName, UserRole role, CancellationToken ct = default)
        {
            LastCreatedRole = role;
            LastCreatedFullName = fullName;
            return Task.FromResult(new AuthUser(UserId, email, [role]));
        }

        public Task<AuthUser> VerifyCredentialsAsync(
            string email, string password, CancellationToken ct = default)
            => FailCredentials
                ? throw new InvalidCredentialsException()
                : Task.FromResult(new AuthUser(UserId, email, [UserRole.Passenger]));

        public Task<AuthUser?> FindByIdAsync(Guid userId, CancellationToken ct = default)
            => Task.FromResult(UserVanished
                ? null
                : new AuthUser(userId, "ada@example.com", [UserRole.Passenger]));
    }

    /// <summary>In-memory stand-in mirroring the real store's contract, including reuse semantics.</summary>
    private sealed class FakeRefreshTokenStore : IRefreshTokenStore
    {
        public readonly List<RefreshToken> Saved = [];
        public readonly List<Guid> RevokedSessions = [];
        private readonly Dictionary<string, RefreshToken> _byHash = [];

        public Task SaveAsync(RefreshToken token, CancellationToken ct = default)
        {
            Saved.Add(token);
            _byHash[token.TokenHash] = token;
            return Task.CompletedTask;
        }

        public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
            => Task.FromResult(_byHash.GetValueOrDefault(tokenHash));

        public Task<RefreshToken> ConsumeAsync(
            string tokenHash, DateTimeOffset now, CancellationToken ct = default)
        {
            var token = _byHash.GetValueOrDefault(tokenHash) ?? throw new InvalidRefreshTokenException();
            token.Consume(now);
            return Task.FromResult(token);
        }

        public Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
        {
            RevokedSessions.Add(sessionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCrypto : IRefreshTokenCrypto
    {
        private int _counter;
        public static string HashOf(string raw) => "hash:" + raw;
        public string GenerateRawToken() => $"raw-{Interlocked.Increment(ref _counter)}";
        public string Hash(string rawToken) => HashOf(rawToken);
    }

    private sealed class FakeTokenGenerator : ITokenGenerator
    {
        public AccessToken GenerateAccessToken(
            Guid userId, string email, IReadOnlyCollection<UserRole> roles)
            => new($"access-for-{userId}", Now.AddMinutes(60));
    }
}
