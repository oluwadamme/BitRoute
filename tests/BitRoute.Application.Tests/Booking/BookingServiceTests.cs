using BitRoute.Application.Booking;
using BitRoute.Application.Common;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Exceptions;
using BitRoute.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BitRoute.Application.Tests.Booking;

public class BookingServiceTests
{
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IRouteRepository> _routeRepositoryMock = new();
    private readonly Mock<IVehicleRepository> _vehicleRepositoryMock = new();
    private readonly Mock<IScheduleRepository> _scheduleRepositoryMock = new();
    private readonly Mock<IBookingRepository> _bookingRepositoryMock = new();
    private readonly Mock<IRequestValidator> _validatorMock = new();
    private readonly Mock<TimeProvider> _clockMock = new();
    private readonly Mock<ILogger<BookingService>> _loggerMock = new();
    private readonly BookingService _service;

    private readonly Route _route;
    private readonly Vehicle _vehicle;
    private readonly Schedule _schedule;
    private readonly Seat _seat1;
    private readonly Seat _seat2;

    public BookingServiceTests()
    {
        _route = Route.Create("Lagos to Ibadan", new[] { "Lagos", "Shagamu", "Ibadan" });
        _vehicle = Vehicle.Create("Van 1", new[] { "S1", "S2" });
        
        _seat1 = _vehicle.Seats.First(s => s.Number == "S1");
        _seat2 = _vehicle.Seats.First(s => s.Number == "S2");

        var legs = new[]
        {
            ScheduleLeg.Create(0, 1, 1000), // Lagos to Shagamu
            ScheduleLeg.Create(1, 2, 1200)  // Shagamu to Ibadan
        };
        _schedule = Schedule.Create(_route.Id, _vehicle.Id, new TimeOnly(8, 0), legs);

        _service = new BookingService(
            _unitOfWorkMock.Object,
            _routeRepositoryMock.Object,
            _vehicleRepositoryMock.Object,
            _scheduleRepositoryMock.Object,
            _bookingRepositoryMock.Object,
            _validatorMock.Object,
            _clockMock.Object,
            _loggerMock.Object);

        _clockMock.Setup(c => c.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        // Standard setup for UoW transaction runner
        _unitOfWorkMock.Setup(u => u.ExecuteSerializableAsync(
            It.IsAny<Func<Task<BookingDto>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Func<Task<BookingDto>> op, CancellationToken ct) => op().GetAwaiter().GetResult());
    }

    [Fact]
    public async Task GetScheduleAvailability_ReturnsAvailabilityAndPrice()
    {
        var travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        _scheduleRepositoryMock.Setup(r => r.GetByIdAsync(_schedule.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_schedule);

        _vehicleRepositoryMock.Setup(r => r.GetSeatsByVehicleIdAsync(_vehicle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { _seat1, _seat2 });

        // Simulate an active hold on S1 for segment [0, 1)
        var existingHold = SeatBooking.CreateHeld(
            Guid.NewGuid(), _schedule.Id, travelDate, _seat1.Id, 0, 1, 1000, "key1");
        _bookingRepositoryMock.Setup(r => r.GetActiveBookingsAsync(_schedule.Id, travelDate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existingHold });

        // Query availability for segment [0, 2)
        var response = await _service.GetScheduleAvailabilityAsync(_schedule.Id, travelDate, 0, 2);

        Assert.True(response.Status);
        Assert.Equal(2200, response.Data!.Price); // 1000 + 1200
        
        // S1 is unavailable because [0, 1) overlaps with [0, 2)
        var s1Availability = response.Data.Seats.First(s => s.SeatId == _seat1.Id);
        Assert.False(s1Availability.IsAvailable);

        // S2 has no bookings, so it should be available
        var s2Availability = response.Data.Seats.First(s => s.SeatId == _seat2.Id);
        Assert.True(s2Availability.IsAvailable);
    }

    [Fact]
    public async Task HoldSeat_NonOverlappingSegments_Succeeds()
    {
        var travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var passengerId = Guid.NewGuid();
        var request = new HoldSeatRequest(_schedule.Id, travelDate, _seat1.Id, 0, 1, "idempotency-1");

        _scheduleRepositoryMock.Setup(r => r.GetByIdAsync(_schedule.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_schedule);

        _vehicleRepositoryMock.Setup(r => r.SeatExistsAsync(_seat1.Id, _vehicle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _bookingRepositoryMock.Setup(r => r.HasOverlapAsync(_schedule.Id, travelDate, _seat1.Id, 0, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var response = await _service.HoldSeatAsync(passengerId, request);

        Assert.True(response.Status);
        Assert.Equal(1000, response.Data!.Price);
        Assert.Equal("Held", response.Data.Status);
        _bookingRepositoryMock.Verify(r => r.Add(It.IsAny<SeatBooking>()), Times.Once);
    }

    [Fact]
    public async Task HoldSeat_OverlappingSegment_ThrowsSeatUnavailableException()
    {
        var travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var request = new HoldSeatRequest(_schedule.Id, travelDate, _seat1.Id, 1, 2, "key2");

        _scheduleRepositoryMock.Setup(r => r.GetByIdAsync(_schedule.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(_schedule);

        _vehicleRepositoryMock.Setup(r => r.SeatExistsAsync(_seat1.Id, _vehicle.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _bookingRepositoryMock.Setup(r => r.HasOverlapAsync(_schedule.Id, travelDate, _seat1.Id, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<SeatUnavailableException>(() => 
            _service.HoldSeatAsync(Guid.NewGuid(), request));
    }

    [Fact]
    public async Task HoldSeat_IdempotentRequest_ReturnsExistingBooking()
    {
        var travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var passengerId = Guid.NewGuid();
        var request = new HoldSeatRequest(_schedule.Id, travelDate, _seat1.Id, 0, 2, "idempotency-key");

        var existingHold = SeatBooking.CreateHeld(
            passengerId, _schedule.Id, travelDate, _seat1.Id, 0, 2, 2200, "idempotency-key");

        _bookingRepositoryMock.Setup(r => r.GetByIdempotencyKeyAsync("idempotency-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingHold);

        var response = await _service.HoldSeatAsync(passengerId, request);

        Assert.True(response.Status);
        Assert.Equal(existingHold.Id, response.Data!.Id);
        Assert.Equal("Seat hold created for 10 minutes.", response.Message);
    }

    [Fact]
    public async Task ConfirmBooking_ValidHold_TransitionsToConfirmed()
    {
        var travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var booking = SeatBooking.CreateHeld(
            Guid.NewGuid(), _schedule.Id, travelDate, _seat1.Id, 0, 2, 2200, "key1");

        _bookingRepositoryMock.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        _clockMock.Setup(c => c.GetUtcNow()).Returns(DateTimeOffset.UtcNow);

        var response = await _service.ConfirmBookingAsync(booking.Id);

        Assert.True(response.Status);
        Assert.Equal(SeatBookingStatus.Confirmed, booking.Status);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmBooking_ExpiredHold_ThrowsHoldExpiredException()
    {
        var travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var booking = SeatBooking.CreateHeld(
            Guid.NewGuid(), _schedule.Id, travelDate, _seat1.Id, 0, 2, 2200, "key1");

        _bookingRepositoryMock.Setup(r => r.GetByIdAsync(booking.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(booking);

        // Advance clock past the hold expiry (10 minutes)
        _clockMock.Setup(c => c.GetUtcNow()).Returns(DateTimeOffset.UtcNow.AddMinutes(11));

        await Assert.ThrowsAsync<HoldExpiredException>(() => 
            _service.ConfirmBookingAsync(booking.Id));

        Assert.Equal(SeatBookingStatus.Expired, booking.Status);
    }
}
