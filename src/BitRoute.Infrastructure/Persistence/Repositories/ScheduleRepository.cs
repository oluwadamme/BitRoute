using BitRoute.Domain.Entities;
using BitRoute.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BitRoute.Infrastructure.Persistence.Repositories;

public sealed class ScheduleRepository : IScheduleRepository
{
    private readonly BitRouteDbContext _db;

    public ScheduleRepository(BitRouteDbContext db)
    {
        _db = db;
    }

    public async Task<Schedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Schedules
            .Include(s => s.Route)
                .ThenInclude(r => r.Stops)
            .Include(s => s.ScheduleLegs)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public void Add(Schedule schedule)
    {
        _db.Schedules.Add(schedule);
    }
}
