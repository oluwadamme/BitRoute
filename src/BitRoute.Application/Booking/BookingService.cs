using BitRoute.Application.Common;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace BitRoute.Application.Booking;

public sealed class BookingService : IBookingService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRouteRepository _routeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IPaystackService _paystackService;
    private readonly IAvailabilityCache _cache;
    private readonly ITelemetryCache _telemetryCache;
    private readonly ITelemetryRepository _telemetryRepository;
    private readonly IRequestValidator _validator;
    private readonly TimeProvider _clock;
    private readonly ILogger<BookingService> _logger;

    public BookingService(
        IUnitOfWork unitOfWork,
        IRouteRepository routeRepository,
        IVehicleRepository vehicleRepository,
        IScheduleRepository scheduleRepository,
        IBookingRepository bookingRepository,
        IPaystackService paystackService,
        IAvailabilityCache cache,
        ITelemetryCache telemetryCache,
        ITelemetryRepository telemetryRepository,
        IRequestValidator validator,
        TimeProvider clock,
        ILogger<BookingService> logger)
    {
        _unitOfWork = unitOfWork;
        _routeRepository = routeRepository;
        _vehicleRepository = vehicleRepository;
        _scheduleRepository = scheduleRepository;
        _bookingRepository = bookingRepository;
        _paystackService = paystackService;
        _cache = cache;
        _telemetryCache = telemetryCache;
        _telemetryRepository = telemetryRepository;
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

    public async Task<ApiResponse<IReadOnlyCollection<RouteDto>>> GetAllRoutesAsync(CancellationToken cancellationToken = default)
    {
        var routes = await _routeRepository.GetAllAsync(cancellationToken);
        var dtos = routes
            .Select(r => new RouteDto(r.Id, r.Name, r.Stops.OrderBy(s => s.Index).Select(s => s.Name).ToList()))
            .ToList();
        return ApiResponse.Success<IReadOnlyCollection<RouteDto>>(dtos, "Routes retrieved successfully.");
    }

    public async Task<ApiResponse<Guid>> CreateVehicleAsync(CreateVehicleRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);

        var layout = new VehicleLayout(request.RowCount, request.SeatsPerRow, request.AisleAfterColumn);
        var placements = request.Seats.Select(s => new SeatPlacement(s.Number, s.Row, s.Column));
        var vehicle = Vehicle.Create(request.Name, layout, placements);
        _vehicleRepository.Add(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse.Success(vehicle.Id, "Vehicle created successfully.");
    }

    public async Task<ApiResponse<VehicleDto>> GetVehicleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(id, cancellationToken);
        if (vehicle is null) throw new VehicleNotFoundException($"Vehicle '{id}' not found.");

        var dto = new VehicleDto(vehicle.Id, vehicle.Name, OrderedSeatNumbers(vehicle));
        return ApiResponse.Success(dto, "Vehicle retrieved successfully.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<VehicleDto>>> GetAllVehiclesAsync(CancellationToken cancellationToken = default)
    {
        var vehicles = await _vehicleRepository.GetAllAsync(cancellationToken);
        var dtos = vehicles
            .Select(v => new VehicleDto(v.Id, v.Name, OrderedSeatNumbers(v)))
            .ToList();
        return ApiResponse.Success<IReadOnlyCollection<VehicleDto>>(dtos, "Vehicles retrieved successfully.");
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

    public async Task<ApiResponse<IReadOnlyCollection<ScheduleDto>>> GetAllSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.GetAllAsync(cancellationToken);
        var dtos = schedules
            .Select(s => new ScheduleDto(
                s.Id,
                s.RouteId,
                s.VehicleId,
                s.DepartureTimeOfDay,
                s.ScheduleLegs.Select(l => new ScheduleLegDto(l.Id, l.StartStopIndex, l.EndStopIndex, l.Fare)).ToList()))
            .ToList();
        return ApiResponse.Success<IReadOnlyCollection<ScheduleDto>>(dtos, "Schedules retrieved successfully.");
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

        var cached = await _cache.GetAsync(scheduleId, travelDate, boardingIndex, alightingIndex, cancellationToken);
        if (cached is not null)
        {
            return ApiResponse.Success(cached, "Availability retrieved successfully");
        }

        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null) throw new ScheduleNotFoundException($"Schedule '{scheduleId}' not found.");

        var layout = await _vehicleRepository.GetLayoutAsync(schedule.VehicleId, cancellationToken);
        if (layout is null) throw new VehicleNotFoundException($"Vehicle '{schedule.VehicleId}' not found.");

        var seats = await _vehicleRepository.GetSeatsByVehicleIdAsync(schedule.VehicleId, cancellationToken);
        var activeBookings = await _bookingRepository.GetActiveBookingsAsync(scheduleId, travelDate, cancellationToken);

        var seatAvailabilities = seats.Select(seat =>
        {
            var isAvailable = !activeBookings.Any(b =>
                b.SeatId == seat.Id &&
                b.BoardingIndex < alightingIndex &&
                b.AlightingIndex > boardingIndex);

            return new SeatAvailabilityDto(seat.Id, seat.Number, isAvailable, seat.Row, seat.Column);
        }).ToList();

        var layoutDto = new VehicleLayoutDto(
            layout.Value.RowCount, layout.Value.SeatsPerRow, layout.Value.AisleAfterColumn);

        var price = schedule.GetPrice(boardingIndex, alightingIndex);
        var response = new ScheduleAvailabilityResponse(scheduleId, travelDate, price, layoutDto, seatAvailabilities);

        await _cache.SetAsync(scheduleId, travelDate, boardingIndex, alightingIndex, response, cancellationToken);

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

        await _cache.InvalidateAsync(request.ScheduleId, request.TravelDate, cancellationToken);
        return ApiResponse.Success(bookingDto, "Seat hold created for 10 minutes.");
    }

    public async Task<ApiResponse<object>> ConfirmBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null) throw new BookingDomainException($"Booking '{bookingId}' not found.");

        booking.Confirm(_clock.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateAsync(booking.ScheduleId, booking.TravelDate, cancellationToken);

        _logger.LogInformation("Booking '{BookingId}' confirmed successfully.", bookingId);
        return ApiResponse.SuccessMessage("Booking confirmed successfully.");
    }

    public async Task<ApiResponse<PaystackInitializeResponse>> InitializePaymentAsync(
        Guid bookingId,
        string email,
        CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null) throw new BookingDomainException($"Booking '{bookingId}' not found.");

        if (booking.Status != SeatBookingStatus.Held)
        {
            throw new BookingDomainException($"Booking '{bookingId}' is not in HELD status (current: {booking.Status}).");
        }

        if (booking.HoldExpiry.HasValue && booking.HoldExpiry.Value < _clock.GetUtcNow())
        {
            booking.Expire();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            throw new HoldExpiredException("Cannot initialize payment for an expired seat hold.");
        }

        var paystackResponse = await _paystackService.InitializeTransactionAsync(
            email,
            booking.Price,
            booking.Id.ToString(),
            cancellationToken);

        return ApiResponse.Success(paystackResponse, "Paystack transaction initialized.");
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

    public async Task<ApiResponse<IReadOnlyCollection<BookingDto>>> GetMyBookingsAsync(Guid passengerId, CancellationToken cancellationToken = default)
    {
        var bookings = await _bookingRepository.GetByPassengerIdAsync(passengerId, cancellationToken);
        var dtos = bookings.Select(MapToDto).ToList();
        return ApiResponse.Success<IReadOnlyCollection<BookingDto>>(dtos, "My bookings retrieved successfully.");
    }

    public async Task<ApiResponse<object>> CancelBookingAsync(Guid bookingId, Guid userId, string userRole, CancellationToken cancellationToken = default)
    {
        var booking = await _bookingRepository.GetByIdAsync(bookingId, cancellationToken);
        if (booking is null) throw new BookingDomainException($"Booking '{bookingId}' not found.");

        if (userRole == nameof(UserRole.Passenger) && booking.PassengerId != userId)
        {
            throw new BookingDomainException("You do not have permission to cancel this booking.");
        }

        booking.Cancel();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _cache.InvalidateAsync(booking.ScheduleId, booking.TravelDate, cancellationToken);

        _logger.LogInformation("Booking '{BookingId}' cancelled successfully by user '{UserId}'.", bookingId, userId);
        return ApiResponse.SuccessMessage("Booking cancelled successfully.");
    }

    /// <summary>
    /// Seat labels in seat-plan order, front-left to back-right. The navigation collection comes
    /// back in whatever order the database chose, which is not stable across requests.
    /// </summary>
    private static List<string> OrderedSeatNumbers(Vehicle vehicle)
        => vehicle.Seats
            .OrderBy(s => s.Row)
            .ThenBy(s => s.Column)
            .Select(s => s.Number)
            .ToList();

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

    public async Task<ApiResponse<TelemetryLocationDto>> GetLatestTelemetryAsync(
        Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var cached = await _telemetryCache.GetLatestAsync(scheduleId, cancellationToken);
        if (cached is not null)
        {
            return ApiResponse.Success(cached, "Latest telemetry location retrieved from cache.");
        }

        var latestLog = await _telemetryRepository.GetLatestByScheduleIdAsync(scheduleId, cancellationToken);
        if (latestLog is null)
        {
            throw new TelemetryNotFoundException($"No telemetry location available for schedule '{scheduleId}'.");
        }

        var dto = new TelemetryLocationDto(
            latestLog.ScheduleId,
            latestLog.Latitude,
            latestLog.Longitude,
            latestLog.CurrentLegIndex,
            latestLog.TimestampUtc);

        await _telemetryCache.SetLatestAsync(scheduleId, dto, cancellationToken);

        return ApiResponse.Success(dto, "Latest telemetry location retrieved from history log.");
    }

    public async Task<ApiResponse<IReadOnlyCollection<TelemetryLocationDto>>> GetTelemetryHistoryAsync(
        Guid scheduleId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var logs = await _telemetryRepository.GetHistoryByScheduleIdAsync(scheduleId, limit, cancellationToken);
        var dtos = logs.Select(l => new TelemetryLocationDto(
            l.ScheduleId,
            l.Latitude,
            l.Longitude,
            l.CurrentLegIndex,
            l.TimestampUtc)).ToList();

        return ApiResponse.Success<IReadOnlyCollection<TelemetryLocationDto>>(dtos, "Telemetry history retrieved successfully.");
    }

    public async Task<ApiResponse<ScheduleAnalyticsDto>> GetScheduleAnalyticsAsync(
        Guid scheduleId, DateOnly travelDate, CancellationToken cancellationToken = default)
    {
        var schedule = await _scheduleRepository.GetByIdAsync(scheduleId, cancellationToken);
        if (schedule is null)
        {
            throw new BookingDomainException($"Schedule '{scheduleId}' not found.");
        }

        var activeBookings = await _bookingRepository.GetActiveBookingsAsync(scheduleId, travelDate, cancellationToken);
        var confirmedBookings = activeBookings.Where(b => b.Status == SeatBookingStatus.Confirmed).ToList();

        var seats = schedule.Vehicle?.Seats;
        if (seats is null || seats.Count == 0)
        {
            var fetchedSeats = await _vehicleRepository.GetSeatsByVehicleIdAsync(schedule.VehicleId, cancellationToken);
            seats = fetchedSeats.ToList();
        }

        var totalCapacity = seats.Count;
        var totalRevenueKobo = confirmedBookings.Sum(b => b.Price);

        var legOccupancies = new List<LegOccupancyDto>();
        var stops = schedule.Route?.Stops.ToDictionary(s => s.Index, s => s.Name) ?? new();

        foreach (var leg in schedule.ScheduleLegs.OrderBy(l => l.StartStopIndex))
        {
            var startName = stops.GetValueOrDefault(leg.StartStopIndex, $"Stop {leg.StartStopIndex}");
            var endName = stops.GetValueOrDefault(leg.EndStopIndex, $"Stop {leg.EndStopIndex}");

            var occupied = activeBookings.Count(b => b.BoardingIndex <= leg.StartStopIndex && b.AlightingIndex >= leg.EndStopIndex);
            var percentage = totalCapacity > 0 ? Math.Round((double)occupied / totalCapacity * 100.0, 1) : 0.0;

            legOccupancies.Add(new LegOccupancyDto(
                leg.StartStopIndex,
                startName,
                endName,
                occupied,
                totalCapacity,
                percentage));
        }

        var dto = new ScheduleAnalyticsDto(
            scheduleId,
            travelDate,
            totalCapacity,
            confirmedBookings.Count,
            totalRevenueKobo,
            legOccupancies);

        return ApiResponse.Success(dto, "Schedule analytics generated successfully.");
    }
}
