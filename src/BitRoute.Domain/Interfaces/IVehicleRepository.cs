using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Vehicle>> GetAllAsync(CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<bool> SeatExistsAsync(Guid seatId, Guid vehicleId, CancellationToken cancellationToken = default);
    
    Task<IReadOnlyCollection<Seat>> GetSeatsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    
    void Add(Vehicle vehicle);
}
