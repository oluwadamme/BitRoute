namespace BitRoute.Domain.Exceptions;

public sealed class VehicleNotFoundException : DomainException
{
    public VehicleNotFoundException(string message) : base(message) { }
}
