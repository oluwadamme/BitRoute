using MediatR;
using Microsoft.Extensions.Logging;

namespace BitRoute.Application.Booking;

public sealed class PaymentConfirmedHandler : INotificationHandler<PaymentConfirmedNotification>
{
    private readonly IBookingService _bookingService;
    private readonly ILogger<PaymentConfirmedHandler> _logger;

    public PaymentConfirmedHandler(
        IBookingService bookingService,
        ILogger<PaymentConfirmedHandler> logger)
    {
        _bookingService = bookingService;
        _logger = logger;
    }

    public async Task Handle(PaymentConfirmedNotification notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Handling PaymentConfirmedNotification for booking '{BookingId}' (Reference: '{Reference}').",
            notification.BookingId, notification.Reference);

        await _bookingService.ConfirmBookingAsync(notification.BookingId, cancellationToken);
    }
}
