namespace BitRoute.Domain.Entities;

public sealed class ScheduleLeg
{
    public Guid Id { get; private set; }
    public Guid ScheduleId { get; private set; }
    public int StartStopIndex { get; private set; }
    public int EndStopIndex { get; private set; }
    public int Fare { get; private set; }

    // Navigation property
    public Schedule Schedule { get; private set; } = null!;

    private ScheduleLeg() { }

    public static ScheduleLeg Create(int startStopIndex, int endStopIndex, int fare)
    {
        if (startStopIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(startStopIndex), "Start stop index cannot be negative.");
        if (endStopIndex <= startStopIndex)
            throw new ArgumentException("End stop index must be greater than start stop index.", nameof(endStopIndex));
        if (fare < 0)
            throw new ArgumentOutOfRangeException(nameof(fare), "Fare cannot be negative.");

        return new ScheduleLeg
        {
            Id = Guid.NewGuid(),
            StartStopIndex = startStopIndex,
            EndStopIndex = endStopIndex,
            Fare = fare
        };
    }
}
