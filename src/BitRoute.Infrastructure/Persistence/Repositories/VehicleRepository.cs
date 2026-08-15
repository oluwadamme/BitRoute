using BitRoute.Domain.Entities;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace BitRoute.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly BitRouteDbContext _db;

    public VehicleRepository(BitRouteDbContext db)
    {
        _db = db;
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .Include(v => v.Seats)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .Include(v => v.Seats)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.Vehicles
            .AnyAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<bool> SeatExistsAsync(Guid seatId, Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await _db.Seats
            .AnyAsync(s => s.Id == seatId && s.VehicleId == vehicleId, cancellationToken);
    }

    public async Task<VehicleLayout?> GetLayoutAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var layout = await _db.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == vehicleId)
            .Select(v => new { v.RowCount, v.SeatsPerRow, v.AisleAfterColumn })
            .FirstOrDefaultAsync(cancellationToken);

        return layout is null
            ? null
            : new VehicleLayout(layout.RowCount, layout.SeatsPerRow, layout.AisleAfterColumn);
    }

    public async Task<IReadOnlyCollection<Seat>> GetSeatsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        // Ordered by seat-plan position, front-left to back-right. Without an explicit ORDER BY
        // this returned rows in Postgres heap order, which can differ between requests and made
        // the seat list unstable for any caller rendering it.
        var list = await _db.Seats
            .AsNoTracking()
            .Where(s => s.VehicleId == vehicleId)
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Column)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public void Add(Vehicle vehicle)
    {
        _db.Vehicles.Add(vehicle);
    }
}
