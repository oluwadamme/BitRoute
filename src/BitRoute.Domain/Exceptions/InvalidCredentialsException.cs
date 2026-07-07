namespace BitRoute.Domain.Exceptions;

/// <summary>
/// Thrown when a login attempt fails. The message is intentionally vague about
/// whether the email or the password was wrong, to avoid revealing which emails
/// have accounts (user enumeration).
/// </summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base("The email or password is incorrect.")
    {
    }
}
