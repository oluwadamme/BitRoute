using MediatR;
using Microsoft.Extensions.Logging;

namespace BitRoute.Application.Booking.Events;

/// <summary>
/// MediatR Notification Handler that processes expired seat holds by invalidating
/// availability caches and logging domain notifications.
/// </summary>
public sealed class SeatHoldExpiredNotificationHandler : INotificationHandler<SeatHoldExpiredEvent>
{
    private readonly IAvailabilityCache _availabilityCache;
    private readonly ILogger<SeatHoldExpiredNotificationHandler> _logger;

    public SeatHoldExpiredNotificationHandler(
        IAvailabilityCache availabilityCache,
        ILogger<SeatHoldExpiredNotificationHandler> logger)
    {
        _availabilityCache = availabilityCache;
        _logger = logger;
    }

    public async Task Handle(SeatHoldExpiredEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing SeatHoldExpiredEvent: Booking '{BookingId}' on Schedule '{ScheduleId}', Seat '{SeatId}' expired at {ExpiredAtUtc}.",
            notification.BookingId,
            notification.ScheduleId,
            notification.SeatId,
            notification.ExpiredAtUtc);

        // Invalidate seat availability cache for the schedule + travel date segment
        await _availabilityCache.InvalidateAsync(notification.ScheduleId, notification.TravelDate, cancellationToken);
    }
}
