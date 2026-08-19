using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.ValueObjects;
using BitRoute.Infrastructure.BackgroundServices;
using BitRoute.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BitRoute.Infrastructure.Tests.BackgroundServices;

public sealed class ExpiredHoldSweeperTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BitRouteDbContext> _options;
    private readonly TestTimeProvider _clock;

    public ExpiredHoldSweeperTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BitRouteDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new BitRouteDbContext(_options);
        db.Database.EnsureCreated();

        _clock = new TestTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private ServiceProvider CreateServiceProvider(BitRouteDbContext db)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        return services.BuildServiceProvider();
    }

    private async Task<(Guid ScheduleId, Guid SeatId)> SeedScheduleAndSeatAsync(BitRouteDbContext db)
    {
        var route = Route.Create("Lagos-Ibadan", new[] { "Lagos", "Ibadan" });
        var layout = new VehicleLayout(4, 3, 2);
        var placements = new[] { new SeatPlacement("1A", 1, 1) };
        var vehicle = Vehicle.Create("Coaster", layout, placements);

        db.Routes.Add(route);
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        var leg = ScheduleLeg.Create(0, 1, 1500);
        var schedule = Schedule.Create(route.Id, vehicle.Id, new TimeOnly(12, 0), new[] { leg });
        db.Schedules.Add(schedule);
        await db.SaveChangesAsync();

        var seat = vehicle.Seats.First();
        return (schedule.Id, seat.Id);
    }

    [Fact]
    public async Task SweepExpiredHoldsAsync_ExpiresOverdueBookings_AndStagesOutboxMessages()
    {
        // Arrange
        using var db = new BitRouteDbContext(_options);
        var (scheduleId, seatId) = await SeedScheduleAndSeatAsync(db);
        var passengerId = Guid.NewGuid();
        var travelDate = new DateOnly(2026, 8, 20);

        var expiredBooking = SeatBooking.CreateHeld(
            passengerId, scheduleId, travelDate, seatId, 0, 1, 1500, "idem-111");

        typeof(SeatBooking).GetProperty(nameof(SeatBooking.HoldExpiry))!
            .SetValue(expiredBooking, _clock.GetUtcNow().AddMinutes(-11));

        db.SeatBookings.Add(expiredBooking);
        await db.SaveChangesAsync();

        using var serviceProvider = CreateServiceProvider(db);
        var sweeper = new ExpiredHoldSweeper(serviceProvider, NullLogger<ExpiredHoldSweeper>.Instance, _clock);

        // Act
        await sweeper.SweepExpiredHoldsAsync();

        // Assert
        using var verifyDb = new BitRouteDbContext(_options);
        var reloadedBooking = await verifyDb.SeatBookings.FindAsync(expiredBooking.Id);
        Assert.NotNull(reloadedBooking);
        Assert.Equal(SeatBookingStatus.Expired, reloadedBooking.Status);

        var outboxMessage = await verifyDb.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(outboxMessage);
        Assert.Contains("SeatHoldExpiredEvent", outboxMessage.Type);
    }

    [Fact]
    public async Task SweepExpiredHoldsAsync_IgnoresActiveHolds()
    {
        // Arrange
        using var db = new BitRouteDbContext(_options);
        var (scheduleId, seatId) = await SeedScheduleAndSeatAsync(db);
        var activeBooking = SeatBooking.CreateHeld(
            Guid.NewGuid(), scheduleId, new DateOnly(2026, 8, 20), seatId, 0, 1, 1500, "idem-active");

        db.SeatBookings.Add(activeBooking);
        await db.SaveChangesAsync();

        // HoldExpiry is set to _clock + 5 mins (in future)
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE seat_bookings SET HoldExpiry = {_clock.GetUtcNow().AddMinutes(5)} WHERE Id = {activeBooking.Id}");

        using var serviceProvider = CreateServiceProvider(db);
        var sweeper = new ExpiredHoldSweeper(serviceProvider, NullLogger<ExpiredHoldSweeper>.Instance, _clock);

        // Act
        await sweeper.SweepExpiredHoldsAsync();

        // Assert
        using var verifyDb = new BitRouteDbContext(_options);
        var reloadedBooking = await verifyDb.SeatBookings.FindAsync(activeBooking.Id);
        Assert.NotNull(reloadedBooking);
        Assert.Equal(SeatBookingStatus.Held, reloadedBooking.Status);

        var outboxCount = await verifyDb.OutboxMessages.CountAsync();
        Assert.Equal(0, outboxCount);
    }
}
