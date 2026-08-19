using BitRoute.Domain.Entities;
using BitRoute.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BitRoute.Infrastructure.Persistence.Repositories;

public sealed class TelemetryRepository : ITelemetryRepository
{
    private readonly BitRouteDbContext _db;

    public TelemetryRepository(BitRouteDbContext db)
    {
        _db = db;
    }

    public async Task<VehicleTelemetryLog?> GetLatestByScheduleIdAsync(
        Guid scheduleId, CancellationToken cancellationToken = default)
    {
        return await _db.VehicleTelemetryLogs
            .Where(t => t.ScheduleId == scheduleId)
            .OrderByDescending(t => t.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<VehicleTelemetryLog>> GetHistoryByScheduleIdAsync(
        Guid scheduleId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var clampedLimit = Math.Clamp(limit, 1, 500);

        var list = await _db.VehicleTelemetryLogs
            .AsNoTracking()
            .Where(t => t.ScheduleId == scheduleId)
            .OrderByDescending(t => t.TimestampUtc)
            .Take(clampedLimit)
            .ToListAsync(cancellationToken);

        return list.OrderBy(t => t.TimestampUtc).ToList().AsReadOnly();
    }
}
