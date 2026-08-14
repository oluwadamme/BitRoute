namespace BitRoute.Domain.Entities;

/// <summary>
/// Domain entity representing a historical GPS location ping from a vehicle on an active schedule departure.
/// </summary>
public sealed class VehicleTelemetryLog
{
    public Guid Id { get; private set; }
    public Guid ScheduleId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int CurrentLegIndex { get; private set; }
    public DateTimeOffset TimestampUtc { get; private set; }

    private VehicleTelemetryLog() { }

    public static VehicleTelemetryLog Create(
        Guid scheduleId,
        double latitude,
        double longitude,
        int currentLegIndex,
        DateTimeOffset timestampUtc)
    {
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("Schedule ID cannot be empty.", nameof(scheduleId));

        return new VehicleTelemetryLog
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            Latitude = latitude,
            Longitude = longitude,
            CurrentLegIndex = currentLegIndex,
            TimestampUtc = timestampUtc
        };
    }
}
