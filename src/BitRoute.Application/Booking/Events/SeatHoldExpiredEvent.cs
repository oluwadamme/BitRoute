using MediatR;

namespace BitRoute.Application.Booking.Events;

/// <summary>
/// Domain notification event published when a 10-minute seat hold expires without payment confirmation.
/// </summary>
public sealed record SeatHoldExpiredEvent(
    Guid BookingId,
    Guid ScheduleId,
    Guid PassengerId,
    DateOnly TravelDate,
    Guid SeatId,
    int BoardingIndex,
    int AlightingIndex,
    DateTimeOffset ExpiredAtUtc
) : INotification;
