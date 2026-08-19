namespace BitRoute.Domain.Exceptions;

public sealed class HoldExpiredException : DomainException
{
    public HoldExpiredException(string message) : base(message) { }
}
