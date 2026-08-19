namespace BitRoute.Domain.Exceptions;

/// <summary>
/// Base type for all domain rule violations. A single base lets the API's
/// exception middleware map domain errors to HTTP status codes in one place,
/// while each concrete exception still carries its own specific meaning.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
