using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

public interface ITelemetryRepository
{
    Task<VehicleTelemetryLog?> GetLatestByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<VehicleTelemetryLog>> GetHistoryByScheduleIdAsync(Guid scheduleId, int limit = 100, CancellationToken cancellationToken = default);
}
