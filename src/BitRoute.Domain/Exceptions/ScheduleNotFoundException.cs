namespace BitRoute.Domain.Exceptions;

public sealed class ScheduleNotFoundException : DomainException
{
    public ScheduleNotFoundException(string message) : base(message) { }
}
