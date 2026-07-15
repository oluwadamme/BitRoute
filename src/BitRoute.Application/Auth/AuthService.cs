using BitRoute.Application.Common;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace BitRoute.Application.Auth;

/// <summary>
/// Orchestrates the auth use cases over the Domain contracts. Pure coordination:
/// the rotation and reuse rules live on <see cref="RefreshToken"/>, atomicity lives
/// in the store, credential checks live behind <see cref="IIdentityService"/>.
/// Validators run here so the rules hold no matter which transport calls in.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IIdentityService _identity;
    private readonly IRefreshTokenStore _store;
    private readonly IRefreshTokenCrypto _crypto;
    private readonly ITokenGenerator _tokens;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _refreshLifetime;
    private readonly IRequestValidator _validator;

    public AuthService(
        IIdentityService identity,
        IRefreshTokenStore store,
        IRefreshTokenCrypto crypto,
        ITokenGenerator tokens,
        TimeProvider clock,
        IOptions<RefreshTokenOptions> options,
        IRequestValidator validator)
    {
        _identity = identity;
        _store = store;
        _crypto = crypto;
        _tokens = tokens;
        _clock = clock;
        _refreshLifetime = options.Value.Lifetime;
        _validator = validator;
    }

    public async Task<AuthResult> RegisterAsync(
        RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // The role is fixed here by design: operators and admins are provisioned,
        // never self-registered. No role ever comes from the request.
        var user = await _identity.CreateUserAsync(
            request.Email, request.Password, request.FullName, UserRole.Passenger, cancellationToken);

        return await StartSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResult> LoginAsync(
        LoginRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var user = await _identity.VerifyCredentialsAsync(
            request.Email, request.Password, cancellationToken);

        return await StartSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(
        RefreshRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var now = _clock.GetUtcNow();
        RefreshToken consumed;
        try
        {
            consumed = await _store.ConsumeAsync(
                _crypto.Hash(request.RefreshToken), now, cancellationToken);
        }
        catch (RefreshTokenReuseException reuse)
        {
            // A replayed token means the family is compromised: revoke everything
            // in the session, then let the failure reach the caller.
            await _store.RevokeSessionAsync(reuse.SessionId, cancellationToken);
            throw;
        }

        var user = await _identity.FindByIdAsync(consumed.UserId, cancellationToken)
            ?? throw new InvalidRefreshTokenException();

        var rawNext = _crypto.GenerateRawToken();
        var next = consumed.IssueNextInSession(_crypto.Hash(rawNext), now, _refreshLifetime);
        await _store.SaveAsync(next, cancellationToken);

        var access = _tokens.GenerateAccessToken(user.Id, user.Email, user.Roles);
        return new AuthResult(access.Token, access.ExpiresAt, rawNext);
    }

    public async Task LogoutAsync(
        LogoutRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        // Logout is idempotent: an unknown or expired token is already logged out.
        var token = await _store.FindByHashAsync(_crypto.Hash(request.RefreshToken), cancellationToken);
        if (token is not null)
        {
            await _store.RevokeSessionAsync(token.SessionId, cancellationToken);
        }
    }

    private async Task<AuthResult> StartSessionAsync(
        AuthUser user, CancellationToken cancellationToken)
    {
        var rawToken = _crypto.GenerateRawToken();
        var refreshToken = RefreshToken.IssueForNewSession(
            user.Id, _crypto.Hash(rawToken), _clock.GetUtcNow(), _refreshLifetime);
        await _store.SaveAsync(refreshToken, cancellationToken);

        var access = _tokens.GenerateAccessToken(user.Id, user.Email, user.Roles);
        return new AuthResult(access.Token, access.ExpiresAt, rawToken);
    }
}
