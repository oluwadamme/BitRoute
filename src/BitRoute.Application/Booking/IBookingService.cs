namespace BitRoute.Application.Booking;

public interface IBookingService
{
    Task<ApiResponse<Guid>> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken = default);
    
    Task<ApiResponse<Guid>> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default);
    
    Task<ApiResponse<Guid>> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<ScheduleAvailabilityResponse>> GetScheduleAvailabilityAsync(
        Guid scheduleId,
        DateOnly travelDate,
        int boardingIndex,
        int alightingIndex,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<BookingDto>> HoldSeatAsync(
        Guid passengerId,
        HoldSeatRequest request,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> ConfirmBookingAsync(
        Guid bookingId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<BookingDto>> GetBookingAsync(
        Guid bookingId,
        Guid userId,
        string userRole,
        CancellationToken cancellationToken = default);
}
