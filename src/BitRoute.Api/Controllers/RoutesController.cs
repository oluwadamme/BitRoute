using BitRoute.Application;
using BitRoute.Application.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitRoute.Api.Controllers;

[ApiController]
[Route("routes")]
public sealed class RoutesController : ControllerBase
{
    private readonly IBookingService _booking;

    public RoutesController(IBookingService booking)
    {
        _booking = booking;
    }

    /// <summary>Creates a new route with its ordered stops (admin/operator only).</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateRoute(
        CreateRouteRequest request, CancellationToken cancellationToken)
    {
        var response = await _booking.CreateRouteAsync(request, cancellationToken);
        return CreatedAtAction(null, new { id = response.Data }, response);
    }
}
