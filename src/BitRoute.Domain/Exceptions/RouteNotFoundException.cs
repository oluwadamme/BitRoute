namespace BitRoute.Domain.Exceptions;

public sealed class RouteNotFoundException : DomainException
{
    public RouteNotFoundException(string message) : base(message) { }
}
