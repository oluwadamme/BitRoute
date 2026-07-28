namespace BitRoute.Domain.Exceptions;

public sealed class SeatUnavailableException : DomainException
{
    public SeatUnavailableException(string message) : base(message) { }
}
