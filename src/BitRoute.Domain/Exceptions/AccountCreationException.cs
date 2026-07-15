namespace BitRoute.Domain.Exceptions;

/// <summary>
/// Thrown when account creation fails for a reason other than a duplicate email
/// (for example a password rejected by the identity system). Carries the human-readable
/// reasons; the API maps this to 400 Bad Request.
/// </summary>
public sealed class AccountCreationException : DomainException
{
    public AccountCreationException(IEnumerable<string> reasons)
        : base($"The account could not be created: {string.Join(" ", reasons)}")
    {
    }
}
