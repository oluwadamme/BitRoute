using MediatR;

namespace BitRoute.Application.Booking;

public sealed record PaymentConfirmedNotification(
    Guid BookingId,
    string Reference,
    int AmountInKobo) : INotification;
