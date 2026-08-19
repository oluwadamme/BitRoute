namespace BitRoute.Domain.Exceptions;

public sealed class InvalidBookingTransitionException : DomainException
{
    public InvalidBookingTransitionException(string message) : base(message) { }
}
