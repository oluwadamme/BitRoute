using BitRoute.Domain.Interfaces;

namespace BitRoute.Application.Booking;

public interface IBookingService
{
    Task<ApiResponse<Guid>> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken = default);
    
    Task<ApiResponse<RouteDto>> GetRouteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyCollection<RouteDto>>> GetAllRoutesAsync(CancellationToken cancellationToken = default);
    
    Task<ApiResponse<Guid>> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default);
    
    Task<ApiResponse<VehicleDto>> GetVehicleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyCollection<VehicleDto>>> GetAllVehiclesAsync(CancellationToken cancellationToken = default);
    
    Task<ApiResponse<Guid>> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default);

    Task<ApiResponse<ScheduleDto>> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyCollection<ScheduleDto>>> GetAllSchedulesAsync(CancellationToken cancellationToken = default);

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

    Task<ApiResponse<PaystackInitializeResponse>> InitializePaymentAsync(
        Guid bookingId,
        string email,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<BookingDto>> GetBookingAsync(
        Guid bookingId,
        Guid userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<IReadOnlyCollection<BookingDto>>> GetMyBookingsAsync(
        Guid passengerId,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<object>> CancelBookingAsync(
        Guid bookingId,
        Guid userId,
        string userRole,
        CancellationToken cancellationToken = default);

    Task<ApiResponse<TelemetryLocationDto>> GetLatestTelemetryAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default);
}
