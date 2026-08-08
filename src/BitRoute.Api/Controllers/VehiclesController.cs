using BitRoute.Application;
using BitRoute.Application.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitRoute.Api.Controllers;

[ApiController]
[Route("vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IBookingService _booking;

    public VehiclesController(IBookingService booking)
    {
        _booking = booking;
    }

    /// <summary>Creates a new vehicle with its seats (admin/operator only).</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateVehicle(
        CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var response = await _booking.CreateVehicleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetVehicle), new { id = response.Data }, response);
    }

    /// <summary>Retrieves details of a vehicle by ID (authenticated users).</summary>
    [Authorize]
    [HttpGet("{id:guid}", Name = nameof(GetVehicle))]
    public async Task<ActionResult<ApiResponse<VehicleDto>>> GetVehicle(
        Guid id, CancellationToken cancellationToken)
    {
        var response = await _booking.GetVehicleAsync(id, cancellationToken);
        return Ok(response);
    }
}
