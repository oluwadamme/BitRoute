namespace BitRoute.Application.Booking;

/// <summary>
/// Interface for caching and invalidating schedule seat availability responses.
/// Defined in Application layer since it deals with application DTOs (ScheduleAvailabilityResponse).
/// Implementation lives in Infrastructure using Redis.
/// </summary>
public interface IAvailabilityCache
{
    Task<ScheduleAvailabilityResponse?> GetAsync(
        Guid scheduleId,
        DateOnly travelDate,
        int boardingIndex,
        int alightingIndex,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        Guid scheduleId,
        DateOnly travelDate,
        int boardingIndex,
        int alightingIndex,
        ScheduleAvailabilityResponse response,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(
        Guid scheduleId,
        DateOnly travelDate,
        CancellationToken cancellationToken = default);
}
