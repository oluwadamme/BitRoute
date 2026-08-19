namespace BitRoute.Domain.Exceptions;

public sealed class BookingDomainException : DomainException
{
    public BookingDomainException(string message) : base(message) { }
}
