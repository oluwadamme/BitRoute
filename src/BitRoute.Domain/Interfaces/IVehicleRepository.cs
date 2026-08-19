using BitRoute.Domain.Entities;
using BitRoute.Domain.ValueObjects;

namespace BitRoute.Domain.Interfaces;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads only the cabin shape (rows, width, aisle) for a vehicle, without loading its seats.
    /// Returns null when the vehicle does not exist.
    /// </summary>
    Task<VehicleLayout?> GetLayoutAsync(Guid vehicleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> SeatExistsAsync(Guid seatId, Guid vehicleId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyCollection<Seat>> GetSeatsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    
    void Add(Vehicle vehicle);
}
