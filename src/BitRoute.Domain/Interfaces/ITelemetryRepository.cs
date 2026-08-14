using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

public interface ITelemetryRepository
{
    Task<VehicleTelemetryLog?> GetLatestByScheduleIdAsync(Guid scheduleId, CancellationToken cancellationToken = default);
}
