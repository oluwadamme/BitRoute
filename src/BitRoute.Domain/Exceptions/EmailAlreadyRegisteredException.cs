namespace BitRoute.Domain.Exceptions;

/// <summary>
/// Thrown when registration is attempted with an email that already has an account.
/// The API maps this to 409 Conflict.
/// </summary>
public sealed class EmailAlreadyRegisteredException : DomainException
{
    public EmailAlreadyRegisteredException(string email)
        : base($"An account with email '{email}' already exists.")
    {
    }
}
