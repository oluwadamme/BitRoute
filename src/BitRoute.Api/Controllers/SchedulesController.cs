using BitRoute.Application;
using BitRoute.Application.Booking;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BitRoute.Api.Controllers;

[ApiController]
[Route("schedules")]
public sealed class SchedulesController : ControllerBase
{
    private readonly IBookingService _booking;

    public SchedulesController(IBookingService booking)
    {
        _booking = booking;
    }

    /// <summary>Creates a new schedule departure template (admin/operator only).</summary>
    [Authorize(Roles = "Admin,Operator")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateSchedule(
        CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var response = await _booking.CreateScheduleAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetSchedule), new { id = response.Data }, response);
    }

    /// <summary>Retrieves details of a schedule by ID (authenticated users).</summary>
    [Authorize]
    [HttpGet("{id:guid}", Name = nameof(GetSchedule))]
    public async Task<ActionResult<ApiResponse<ScheduleDto>>> GetSchedule(
        Guid id, CancellationToken cancellationToken)
    {
        var response = await _booking.GetScheduleAsync(id, cancellationToken);
        return Ok(response);
    }

    /// <summary>Lists all available schedule departures.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ScheduleDto>>>> GetAllSchedules(
        CancellationToken cancellationToken)
    {
        var response = await _booking.GetAllSchedulesAsync(cancellationToken);
        return Ok(response);
    }

    /// <summary>Retrieves available seats and price for a schedule on a travel date.</summary>
    [HttpGet("{scheduleId:guid}/availability")]
    public async Task<ActionResult<ApiResponse<ScheduleAvailabilityResponse>>> GetAvailability(
        Guid scheduleId,
        [FromQuery] DateOnly travelDate,
        [FromQuery] int boardingIndex,
        [FromQuery] int alightingIndex,
        CancellationToken cancellationToken)
    {
        var response = await _booking.GetScheduleAvailabilityAsync(
            scheduleId, travelDate, boardingIndex, alightingIndex, cancellationToken);
        return Ok(response);
    }

    /// <summary>Retrieves the latest known GPS location ping for a schedule departure.</summary>
    [HttpGet("{id:guid}/telemetry/latest")]
    public async Task<ActionResult<ApiResponse<TelemetryLocationDto>>> GetLatestTelemetry(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await _booking.GetLatestTelemetryAsync(id, cancellationToken);
        return Ok(response);
    }
}
