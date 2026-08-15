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

/// <summary>
/// A seat and where it physically sits. Row is 1-based from the front of the vehicle; Column is
/// 1-based counting left-to-right across the full width INCLUDING the aisle position, so a 2+2
/// coach uses columns 1, 2, 4, 5 and leaves column 3 empty as the aisle.
/// </summary>
public sealed record CreateSeatDto(string Number, int Row, int Column);

/// <summary>
/// Request to create a Vehicle. SeatsPerRow is the total width of a row including the column the
/// aisle occupies (2+2 coach = 5, 2+1 van = 4); AisleAfterColumn is the column the aisle sits
/// immediately after, or null when the vehicle has no aisle.
/// </summary>
/// <example>
/// {
///   "name": "Luxury Coach 1",
///   "rowCount": 3,
///   "seatsPerRow": 5,
///   "aisleAfterColumn": 2,
///   "seats": [
///     { "number": "1A", "row": 1, "column": 1 },
///     { "number": "1B", "row": 1, "column": 2 },
///     { "number": "1C", "row": 1, "column": 4 },
///     { "number": "1D", "row": 1, "column": 5 },
///     { "number": "2A", "row": 2, "column": 1 },
///     { "number": "2B", "row": 2, "column": 2 },
///     { "number": "2C", "row": 2, "column": 4 },
///     { "number": "2D", "row": 2, "column": 5 },
///     { "number": "3A", "row": 3, "column": 1 },
///     { "number": "3B", "row": 3, "column": 2 },
///     { "number": "3C", "row": 3, "column": 4 },
///     { "number": "3D", "row": 3, "column": 5 }
///   ]
/// }
/// </example>
public sealed record CreateVehicleRequest(
    string Name,
    int RowCount,
    int SeatsPerRow,
    int? AisleAfterColumn,
    List<CreateSeatDto> Seats);

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

/// <summary>Request to initialize a payment.</summary>
public sealed record InitializePaymentRequest(string Email);

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
    Guid PassengerId,
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

/// <summary>
/// A seat, whether it is free for the requested segment, and where it sits in the vehicle.
/// Row and Column are 1-based; Column counts across the full cabin width including the aisle
/// position, so a 2+2 coach reports columns 1, 2, 4, 5.
/// </summary>
public sealed record SeatAvailabilityDto(
    Guid SeatId, string SeatNumber, bool IsAvailable, int Row, int Column);

/// <summary>
/// The shape of the cabin the seats are laid out on. SeatsPerRow includes the aisle column;
/// AisleAfterColumn is null when the vehicle has no aisle.
/// </summary>
public sealed record VehicleLayoutDto(int RowCount, int SeatsPerRow, int? AisleAfterColumn);

/// <summary>Availability query response containing the seat plan, free seats, and fare.</summary>
public sealed record ScheduleAvailabilityResponse(
    Guid ScheduleId,
    DateOnly TravelDate,
    int Price,
    VehicleLayoutDto Layout,
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

/// <summary>Latest known GPS location for a schedule departure.</summary>
public sealed record TelemetryLocationDto(
    Guid ScheduleId,
    double Latitude,
    double Longitude,
    int CurrentLegIndex,
    DateTimeOffset Timestamp);
