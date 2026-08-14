using BitRoute.Domain.Entities;
using BitRoute.Domain.Interfaces;
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

    public async Task<IReadOnlyCollection<Seat>> GetSeatsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var list = await _db.Seats
            .Where(s => s.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public void Add(Vehicle vehicle)
    {
        _db.Vehicles.Add(vehicle);
    }
}
