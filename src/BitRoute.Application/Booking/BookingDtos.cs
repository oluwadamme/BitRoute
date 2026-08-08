using BitRoute.Domain.Enums;

namespace BitRoute.Application.Booking;

/// <summary>Request to create a Route.</summary>
/// <example>
/// {
///   "name": "Lagos to Abuja",
///   "stops": ["Lagos", "Ibadan", "Lokoja", "Abuja"]
/// }
/// </example>
public sealed record CreateRouteRequest(string Name, List<string> Stops);

/// <summary>Request to create a Vehicle.</summary>
/// <example>
/// {
///   "name": "Luxury Coach 1",
///   "seats": ["1A", "1B", "2A", "2B", "3A", "3B"]
/// }
/// </example>
public sealed record CreateVehicleRequest(string Name, List<string> Seats);

/// <summary>Leg definition for creating a schedule.</summary>
public sealed record CreateScheduleLegDto(int StartStopIndex, int EndStopIndex, int Fare);

/// <summary>Request to create a Schedule.</summary>
/// <example>
/// {
///   "routeId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "vehicleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "departureTimeOfDay": "08:00:00",
///   "legs": [
///     { "startStopIndex": 0, "endStopIndex": 1, "fare": 1000 },
///     { "startStopIndex": 1, "endStopIndex": 2, "fare": 1500 },
///     { "startStopIndex": 2, "endStopIndex": 3, "fare": 2000 }
///   ]
/// }
/// </example>
public sealed record CreateScheduleRequest(
    Guid RouteId,
    Guid VehicleId,
    TimeOnly DepartureTimeOfDay,
    List<CreateScheduleLegDto> Legs);

/// <summary>Request to hold a seat.</summary>
/// <example>
/// {
///   "scheduleId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "travelDate": "2026-08-01",
///   "seatId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "boardingIndex": 0,
///   "alightingIndex": 3,
///   "idempotencyKey": "unique-booking-idempotency-key"
/// }
/// </example>
public sealed record HoldSeatRequest(
    Guid ScheduleId,
    DateOnly TravelDate,
    Guid SeatId,
    int BoardingIndex,
    int AlightingIndex,
    string IdempotencyKey);

/// <summary>Details of a Seat hold booking.</summary>
public sealed record BookingDto(
    Guid Id,
    Guid PassengerId,
    Guid ScheduleId,
    DateOnly TravelDate,
    Guid SeatId,
    int BoardingIndex,
    int AlightingIndex,
    string Status,
    int Price,
    DateTimeOffset? HoldExpiry);

/// <summary>Availability query response containing free seats and fare.</summary>
public sealed record SeatAvailabilityDto(Guid SeatId, string SeatNumber, bool IsAvailable);

public sealed record ScheduleAvailabilityResponse(
    Guid ScheduleId,
    DateOnly TravelDate,
    int Price,
    List<SeatAvailabilityDto> Seats);

/// <summary>Details of a Route.</summary>
public sealed record RouteDto(Guid Id, string Name, List<string> Stops);

/// <summary>Details of a Vehicle.</summary>
public sealed record VehicleDto(Guid Id, string Name, List<string> Seats);

/// <summary>Details of a Schedule Leg.</summary>
public sealed record ScheduleLegDto(Guid Id, int StartStopIndex, int EndStopIndex, int Fare);

/// <summary>Details of a Schedule.</summary>
public sealed record ScheduleDto(
    Guid Id,
    Guid RouteId,
    Guid VehicleId,
    TimeOnly DepartureTimeOfDay,
    List<ScheduleLegDto> Legs);
