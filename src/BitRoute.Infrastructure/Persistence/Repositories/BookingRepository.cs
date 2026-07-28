using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BitRoute.Infrastructure.Persistence.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly BitRouteDbContext _db;

    public BookingRepository(BitRouteDbContext db)
    {
        _db = db;
    }

    public async Task<SeatBooking?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _db.SeatBookings
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
    }

    public async Task<SeatBooking?> GetByIdempotencyKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _db.SeatBookings
            .FirstOrDefaultAsync(b => b.IdempotencyKey == key, cancellationToken);
    }

    public async Task<bool> HasOverlapAsync(
        Guid scheduleId,
        DateOnly travelDate,
        Guid seatId,
        int boardingIndex,
        int alightingIndex,
        CancellationToken cancellationToken = default)
    {
        return await _db.SeatBookings
            .AnyAsync(b => b.ScheduleId == scheduleId
                        && b.TravelDate == travelDate
                        && b.SeatId == seatId
                        && (b.Status == SeatBookingStatus.Held || b.Status == SeatBookingStatus.Confirmed)
                        && b.BoardingIndex < alightingIndex
                        && b.AlightingIndex > boardingIndex, cancellationToken);
    }

    public void Add(SeatBooking booking)
    {
        _db.SeatBookings.Add(booking);
    }

    public async Task<IReadOnlyCollection<SeatBooking>> GetActiveBookingsAsync(
        Guid scheduleId,
        DateOnly travelDate,
        CancellationToken cancellationToken = default)
    {
        var list = await _db.SeatBookings
            .Where(b => b.ScheduleId == scheduleId
                     && b.TravelDate == travelDate
                     && (b.Status == SeatBookingStatus.Held || b.Status == SeatBookingStatus.Confirmed))
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }

    public async Task<IReadOnlyCollection<SeatBooking>> GetExpiredHoldsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default)
    {
        var list = await _db.SeatBookings
            .Where(b => b.Status == SeatBookingStatus.Held && b.HoldExpiry.HasValue && b.HoldExpiry.Value < utcNow)
            .ToListAsync(cancellationToken);

        return list.AsReadOnly();
    }
}
