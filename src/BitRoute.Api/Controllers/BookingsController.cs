using System.Security.Claims;
using BitRoute.Application;
using BitRoute.Application.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

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
    [HttpPost("hold")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> HoldSeat(
        HoldSeatRequest request, CancellationToken cancellationToken)
    {
        var passengerId = GetUserId();
        var response = await _booking.HoldSeatAsync(passengerId, request, cancellationToken);
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

    /// <summary>Retrieves details of a specific seat booking (ownership-aware).</summary>
    [Authorize]
    [HttpGet("{bookingId:guid}")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> GetBooking(
        Guid bookingId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var userRole = GetUserRole();
        var response = await _booking.GetBookingAsync(bookingId, userId, userRole, cancellationToken);
        return Ok(response);
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? User.Identity?.Name;

        if (Guid.TryParse(sub, out var userId)) return userId;
        throw new InvalidOperationException("Could not resolve user ID from authenticated token claims.");
    }

    private string GetUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value 
            ?? User.FindFirst("role")?.Value 
            ?? string.Empty;
    }
}
