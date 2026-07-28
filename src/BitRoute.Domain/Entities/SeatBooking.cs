using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;

namespace BitRoute.Domain.Entities;

public sealed class SeatBooking
{
    public Guid Id { get; private set; }
    public Guid PassengerId { get; private set; }
    public Guid ScheduleId { get; private set; }
    public DateOnly TravelDate { get; private set; }
    public Guid SeatId { get; private set; }
    public int BoardingIndex { get; private set; }
    public int AlightingIndex { get; private set; }
    public SeatBookingStatus Status { get; private set; }
    public int Price { get; private set; }
    public DateTimeOffset? HoldExpiry { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;

    // Navigation properties
    public Schedule Schedule { get; private set; } = null!;
    public Seat Seat { get; private set; } = null!;

    private SeatBooking() { }

    public static SeatBooking CreateHeld(
        Guid passengerId,
        Guid scheduleId,
        DateOnly travelDate,
        Guid seatId,
        int boardingIndex,
        int alightingIndex,
        int price,
        string idempotencyKey)
    {
        if (passengerId == Guid.Empty)
            throw new ArgumentException("PassengerId cannot be empty.", nameof(passengerId));
        if (scheduleId == Guid.Empty)
            throw new ArgumentException("ScheduleId cannot be empty.", nameof(scheduleId));
        if (seatId == Guid.Empty)
            throw new ArgumentException("SeatId cannot be empty.", nameof(seatId));
        if (boardingIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(boardingIndex));
        if (alightingIndex <= boardingIndex)
            throw new ArgumentException("Alighting index must be greater than boarding index.");
        if (price < 0)
            throw new ArgumentOutOfRangeException(nameof(price));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key cannot be empty.", nameof(idempotencyKey));

        return new SeatBooking
        {
            Id = Guid.NewGuid(),
            PassengerId = passengerId,
            ScheduleId = scheduleId,
            TravelDate = travelDate,
            SeatId = seatId,
            BoardingIndex = boardingIndex,
            AlightingIndex = alightingIndex,
            Status = SeatBookingStatus.Held,
            Price = price,
            HoldExpiry = DateTimeOffset.UtcNow.AddMinutes(10), // 10-minute hold duration
            IdempotencyKey = idempotencyKey.Trim()
        };
    }

    public void Confirm(DateTimeOffset utcNow)
    {
        if (Status != SeatBookingStatus.Held)
        {
            throw new InvalidBookingTransitionException(
                $"Cannot confirm booking. Current status is {Status}, but only HELD bookings can be confirmed.");
        }

        if (HoldExpiry.HasValue && HoldExpiry.Value < utcNow)
        {
            Status = SeatBookingStatus.Expired;
            HoldExpiry = null;
            throw new HoldExpiredException("Cannot confirm booking because the 10-minute hold has already expired.");
        }

        Status = SeatBookingStatus.Confirmed;
        HoldExpiry = null;
    }

    public void Expire()
    {
        if (Status != SeatBookingStatus.Held)
        {
            throw new InvalidBookingTransitionException(
                $"Cannot expire booking. Current status is {Status}, but only HELD bookings can be expired.");
        }

        Status = SeatBookingStatus.Expired;
        HoldExpiry = null;
    }

    public void Cancel()
    {
        if (Status != SeatBookingStatus.Confirmed)
        {
            throw new InvalidBookingTransitionException(
                $"Cannot cancel booking. Current status is {Status}, but only CONFIRMED bookings can be cancelled.");
        }

        Status = SeatBookingStatus.Cancelled;
    }
}
