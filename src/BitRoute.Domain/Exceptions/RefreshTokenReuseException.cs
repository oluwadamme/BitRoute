namespace BitRoute.Domain.Exceptions;

/// <summary>
/// Thrown when a refresh token that was already consumed is presented again. This is
/// a theft signal: the current token has moved on, so an old one turning up means it
/// leaked. It carries the session id so the caller can revoke the whole family.
/// </summary>
public sealed class RefreshTokenReuseException : DomainException
{
    public Guid SessionId { get; }

    public RefreshTokenReuseException(Guid sessionId)
        : base("The refresh token has already been used. The session has been revoked.")
    {
        SessionId = sessionId;
    }
}
