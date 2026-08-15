using BitRoute.Application;
using BitRoute.Application.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BitRoute.Api.Controllers;

[ApiController]
[Route("bookings")]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _booking;

    public BookingsController(IBookingService booking)
    {
        _booking = booking;
    }

    /// <summary>Creates a temporary 10-minute seat hold (authenticated passenger).</summary>
    [Authorize]
    [EnableRateLimiting("HoldPolicy")]
    [HttpPost("hold")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> HoldSeat(
        HoldSeatRequest request, CancellationToken cancellationToken)
    {
        var response = await _booking.HoldSeatAsync(request.PassengerId, request, cancellationToken);
        return Ok(response);
    }

    /// <summary>Simulates payment confirmation to finalize the seat booking (authenticated users).</summary>
    [Authorize]
    [HttpPost("{bookingId:guid}/confirm")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmBooking(
        Guid bookingId, CancellationToken cancellationToken)
    {
        var response = await _booking.ConfirmBookingAsync(bookingId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Initializes a Paystack transaction for a held seat booking (authenticated passenger).</summary>
    [Authorize]
    [HttpPost("{bookingId:guid}/pay")]
    public async Task<ActionResult<ApiResponse<BitRoute.Domain.Interfaces.PaystackInitializeResponse>>> InitializePaystackPayment(
        Guid bookingId,
        [FromBody] InitializePaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await _booking.InitializePaymentAsync(bookingId, request.Email, cancellationToken);
        return Ok(response);
    }

    /// <summary>Retrieves details of a specific seat booking (ownership-aware).</summary>
    [Authorize]
    [HttpGet("{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> GetBooking(
        Guid bookingId,
        [FromQuery] Guid userId,
        [FromQuery] string userRole,
        CancellationToken cancellationToken)
    {
        var response = await _booking.GetBookingAsync(bookingId, userId, userRole, cancellationToken);
        return Ok(response);
    }

    /// <summary>Retrieves all seat bookings belonging to the authenticated passenger.</summary>
    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<BookingDto>>>> GetMyBookings(
        [FromQuery] Guid passengerId,
        CancellationToken cancellationToken)
    {
        var response = await _booking.GetMyBookingsAsync(passengerId, cancellationToken);
        return Ok(response);
    }

    /// <summary>Cancels an existing booking and releases its seat inventory (ownership-aware).</summary>
    [Authorize]
    [HttpDelete("{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> CancelBooking(
        Guid bookingId,
        [FromQuery] Guid userId,
        [FromQuery] string userRole,
        CancellationToken cancellationToken)
    {
        var response = await _booking.CancelBookingAsync(bookingId, userId, userRole, cancellationToken);
        return Ok(response);
    }
}
