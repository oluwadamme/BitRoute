using BitRoute.Domain.Entities;
using BitRoute.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BitRoute.Infrastructure.Persistence.Repositories;

public sealed class RouteRepository : IRouteRepository
{
    private readonly BitRouteDbContext _db;

    public RouteRepository(BitRouteDbContext db)
    {
        _db = db;
    }

    public async Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Route>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Routes
            .Include(r => r.Stops)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Routes
            .AnyAsync(r => r.Id == id, cancellationToken);
    }

    public void Add(Route route)
    {
        _db.Routes.Add(route);
    }
}
