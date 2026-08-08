using BitRoute.Application.Common;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace BitRoute.Application.Booking;

public sealed class BookingService : IBookingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRouteRepository _routeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IRequestValidator _validator;
    private readonly TimeProvider _clock;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IUnitOfWork unitOfWork,
        IRouteRepository routeRepository,
        IVehicleRepository vehicleRepository,
        IScheduleRepository scheduleRepository,
        IBookingRepository bookingRepository,
        IRequestValidator validator,
        TimeProvider clock,
        ILogger<BookingService> logger)
    {
        _unitOfWork = unitOfWork;
        _routeRepository = routeRepository;
        _vehicleRepository = vehicleRepository;
        _scheduleRepository = scheduleRepository;
        _bookingRepository = bookingRepository;
        _validator = validator;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ApiResponse<Guid>> CreateRouteAsync(CreateRouteRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var route = Route.Create(request.Name, request.Stops);
        _routeRepository.Add(route);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse.Success(route.Id, "Route created successfully.");
    }

    public async Task<ApiResponse<RouteDto>> GetRouteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = await _routeRepository.GetByIdAsync(id, cancellationToken);
        if (route is null) throw new RouteNotFoundException($"Route '{id}' not found.");

        var dto = new RouteDto(route.Id, route.Name, route.Stops.OrderBy(s => s.Index).Select(s => s.Name).ToList());
        return ApiResponse.Success(dto, "Route retrieved successfully.");
    }

    public async Task<ApiResponse<Guid>> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var vehicle = Vehicle.Create(request.Name, request.Seats);
        _vehicleRepository.Add(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse.Success(vehicle.Id, "Vehicle created successfully.");
    }

    public async Task<ApiResponse<VehicleDto>> GetVehicleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (vehicle is null) throw new VehicleNotFoundException($"Vehicle '{id}' not found.");

        var dto = new VehicleDto(vehicle.Id, vehicle.Name, vehicle.Seats.Select(s => s.Number).ToList());
        return ApiResponse.Success(dto, "Vehicle retrieved successfully.");
    }

    public async Task<ApiResponse<Guid>> CreateScheduleAsync(CreateScheduleRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var routeExists = await _routeRepository.ExistsAsync(request.RouteId, cancellationToken);
        if (!routeExists) throw new RouteNotFoundException($"Route '{request.RouteId}' not found.");

        var vehicleExists = await _vehicleRepository.ExistsAsync(request.VehicleId, cancellationToken);
        if (!vehicleExists) throw new VehicleNotFoundException($"Vehicle '{request.VehicleId}' not found.");

        var legs = request.Legs.Select(l => ScheduleLeg.Create(l.StartStopIndex, l.EndStopIndex, l.Fare));
        var schedule = Schedule.Create(request.RouteId, request.VehicleId, request.DepartureTimeOfDay, legs);

        _scheduleRepository.Add(schedule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse.Success(schedule.Id, "Schedule created successfully.");
    }

    public async Task<ApiResponse<ScheduleDto>> GetScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(id, cancellationToken);
        if (schedule is null) throw new ScheduleNotFoundException($"Schedule '{id}' not found.");

        var legDtos = schedule.ScheduleLegs
            .Select(l => new ScheduleLegDto(l.Id, l.StartStopIndex, l.EndStopIndex, l.Fare))
            .ToList();
        var dto = new ScheduleDto(schedule.Id, schedule.RouteId, schedule.VehicleId, schedule.DepartureTimeOfDay, legDtos);
        return ApiResponse.Success(dto, "Schedule retrieved successfully.");
    }

    public async Task<ApiResponse<ScheduleAvailabilityResponse>> GetScheduleAvailabilityAsync(
        Guid scheduleId,
        DateOnly travelDate,
        int boardingIndex,
        int alightingIndex,
        CancellationToken cancellationToken = default)
    {
        if (boardingIndex < 0)
            throw new BookingDomainException("Boarding index cannot be negative.");
        if (alightingIndex <= boardingIndex)
            throw new BookingDomainException("Alighting index must be greater than boarding index.");

        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null) throw new ScheduleNotFoundException($"Schedule '{scheduleId}' not found.");

        var seats = await _vehicleRepository.GetSeatsByVehicleIdAsync(schedule.VehicleId, cancellationToken);
        var activeBookings = await _bookingRepository.GetActiveBookingsAsync(scheduleId, travelDate, cancellationToken);

        var seatAvailabilities = seats.Select(seat =>
        {
            var isAvailable = !activeBookings.Any(b =>
                b.SeatId == seat.Id &&
                b.BoardingIndex < alightingIndex &&
                b.AlightingIndex > boardingIndex);

            return new SeatAvailabilityDto(seat.Id, seat.Number, isAvailable);
        }).ToList();

        var price = schedule.GetPrice(boardingIndex, alightingIndex);
        var response = new ScheduleAvailabilityResponse(scheduleId, travelDate, price, seatAvailabilities);

        return ApiResponse.Success(response, "Availability retrieved successfully.");
    }

    public async Task<ApiResponse<BookingDto>> HoldSeatAsync(
        Guid passengerId,
        HoldSeatRequest request,
        CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var bookingDto = await _unitOfWork.ExecuteSerializableAsync(async () =>
        {
            // 1. Idempotency Check
            var existingBooking = await _bookingRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);

            if (existingBooking is not null)
            {
                if (existingBooking.PassengerId != passengerId ||
                    existingBooking.ScheduleId != request.ScheduleId ||
                    existingBooking.SeatId != request.SeatId ||
                    existingBooking.TravelDate != request.TravelDate ||
                    existingBooking.BoardingIndex != request.BoardingIndex ||
                    existingBooking.AlightingIndex != request.AlightingIndex)
                {
                    throw new BookingDomainException("Idempotency key conflict: identical key reused with different parameters.");
                }

                _logger.LogInformation("Idempotent hold check passed for booking '{BookingId}'", existingBooking.Id);
                return MapToDto(existingBooking);
            }

            // 2. Fetch Schedule and Seats
            var schedule = await _scheduleRepository.GetByIdAsync(request.ScheduleId, cancellationToken);
            if (schedule is null) throw new ScheduleNotFoundException($"Schedule '{request.ScheduleId}' not found.");

            var seatExists = await _vehicleRepository.SeatExistsAsync(request.SeatId, schedule.VehicleId, cancellationToken);
            if (!seatExists) throw new BookingDomainException($"Seat '{request.SeatId}' does not exist on this schedule's vehicle.");

            // 3. Check Overlaps
            var hasOverlap = await _bookingRepository.HasOverlapAsync(
                request.ScheduleId, request.TravelDate, request.SeatId, request.BoardingIndex, request.AlightingIndex, cancellationToken);

            if (hasOverlap)
            {
                throw new SeatUnavailableException("The requested seat is already booked or held for this segment.");
            }

            // 4. Calculate Price & Create Hold
            var price = schedule.GetPrice(request.BoardingIndex, request.AlightingIndex);
            var booking = SeatBooking.CreateHeld(
                passengerId,
                request.ScheduleId,
                request.TravelDate,
                request.SeatId,
                request.BoardingIndex,
                request.AlightingIndex,
                price,
                request.IdempotencyKey);

            _bookingRepository.Add(booking);
            return MapToDto(booking);
        }, cancellationToken);

        return ApiResponse.Success(bookingDto, "Seat hold created for 10 minutes.");
    }

    public async Task<ApiResponse<object>> ConfirmBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null) throw new BookingDomainException($"Booking '{bookingId}' not found.");

        booking.Confirm(_clock.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Booking '{BookingId}' confirmed successfully.", bookingId);
        return ApiResponse.SuccessMessage("Booking confirmed successfully.");
    }

    public async Task<ApiResponse<BookingDto>> GetBookingAsync(Guid bookingId, Guid userId, string userRole, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null) throw new BookingDomainException($"Booking '{bookingId}' not found.");

        // Ownership validation: passengers can only read their own bookings.
        if (userRole == nameof(UserRole.Passenger) && booking.PassengerId != userId)
        {
            throw new BookingDomainException("You do not have permission to view this booking.");
        }

        return ApiResponse.Success(MapToDto(booking), "Booking retrieved successfully.");
    }

    private static BookingDto MapToDto(SeatBooking b)
        => new(
            b.Id,
            b.PassengerId,
            b.ScheduleId,
            b.TravelDate,
            b.SeatId,
            b.BoardingIndex,
            b.AlightingIndex,
            b.Status.ToString(),
            b.Price,
            b.HoldExpiry);
}
