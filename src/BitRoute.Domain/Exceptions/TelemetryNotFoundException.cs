namespace BitRoute.Domain.Exceptions;

public sealed class TelemetryNotFoundException : DomainException
{
    public TelemetryNotFoundException(string message) : base(message)
    {
    }
}
