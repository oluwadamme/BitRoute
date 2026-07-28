using BitRoute.Domain.Entities;

namespace BitRoute.Domain.Interfaces;

public interface IBookingRepository
{
    Task<SeatBooking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<SeatBooking?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default);
    
    Task<bool> HasOverlapAsync(
        Guid scheduleId,
        DateOnly travelDate,
        Guid seatId,
        int boardingIndex,
        int alightingIndex,
        CancellationToken cancellationToken = default);
    
    void Add(SeatBooking booking);
    
    Task<IReadOnlyCollection<SeatBooking>> GetActiveBookingsAsync(
        Guid scheduleId,
        DateOnly travelDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SeatBooking>> GetExpiredHoldsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
