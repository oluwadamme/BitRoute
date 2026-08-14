namespace BitRoute.Application.Booking;

/// <summary>
/// Cache port for latest telemetry locations.
/// Defined in Application because it deals with application DTOs.
/// Implemented in Infrastructure (Redis).
/// </summary>
public interface ITelemetryCache
{
    Task<TelemetryLocationDto?> GetLatestAsync(Guid scheduleId, CancellationToken cancellationToken = default);

    Task SetLatestAsync(Guid scheduleId, TelemetryLocationDto dto, CancellationToken cancellationToken = default);
}
