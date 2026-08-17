using System.Text.Json;
using BitRoute.Application.Booking.Events;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BitRoute.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically sweeps and expires seat bookings whose 10-minute hold window has elapsed.
/// Transitioning to EXPIRED and enqueuing an OutboxMessage occur within a single database transaction.
/// </summary>
public sealed class ExpiredHoldSweeper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeProvider _clock;
    private readonly ILogger<ExpiredHoldSweeper> _logger;
    private readonly TimeSpan _period = TimeSpan.FromSeconds(30);

    public ExpiredHoldSweeper(
        IServiceProvider serviceProvider,
        ILogger<ExpiredHoldSweeper> logger,
        TimeProvider? clock = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Expired Hold Sweeper background service started.");

        using var timer = new PeriodicTimer(_period);
        while (await timer.WaitForNextTickAsync(stoppingToken) && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepExpiredHoldsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during expired hold sweep execution.");
            }
        }
    }

    public async Task SweepExpiredHoldsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();

        var utcNow = _clock.GetUtcNow();

        var activeHeldBookings = await db.SeatBookings
            .Where(b => b.Status == SeatBookingStatus.Held)
            .Take(100)
            .ToListAsync(cancellationToken);

        var expiredBookings = activeHeldBookings
            .Where(b => b.HoldExpiry.HasValue && b.HoldExpiry.Value <= utcNow)
            .ToList();

        if (expiredBookings.Count == 0) return;

        _logger.LogInformation("Sweeper found {Count} expired held seat booking(s). Transitioning to EXPIRED.", expiredBookings.Count);

        var successfullyExpired = new List<SeatBooking>();

        foreach (var booking in expiredBookings)
        {
            try
            {
                booking.Expire();
                successfullyExpired.Add(booking);

                // Stage outbox event for asynchronous notification / cache processing
                var expiredEvent = new SeatHoldExpiredEvent(
                    booking.Id,
                    booking.ScheduleId,
                    booking.PassengerId,
                    booking.TravelDate,
                    booking.SeatId,
                    booking.BoardingIndex,
                    booking.AlightingIndex,
                    utcNow);

                var outboxMessage = OutboxMessage.Create(
                    typeof(SeatHoldExpiredEvent).AssemblyQualifiedName ?? typeof(SeatHoldExpiredEvent).FullName!,
                    JsonSerializer.Serialize(expiredEvent),
                    utcNow);

                db.OutboxMessages.Add(outboxMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to transition booking '{BookingId}' to EXPIRED.", booking.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var cache = scope.ServiceProvider.GetService<BitRoute.Application.Booking.IAvailabilityCache>();
        if (cache is not null)
        {
            foreach (var group in successfullyExpired.GroupBy(b => (b.ScheduleId, b.TravelDate)))
            {
                await cache.InvalidateAsync(group.Key.ScheduleId, group.Key.TravelDate, cancellationToken);
            }
        }
    }
}
