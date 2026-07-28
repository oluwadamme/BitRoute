using BitRoute.Domain.Enums;
using BitRoute.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BitRoute.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that periodically sweeps and expires seat bookings whose 10-minute hold window has elapsed.
/// Runs every 30 seconds.
/// </summary>
public sealed class ExpiredHoldSweeper : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExpiredHoldSweeper> _logger;
    private readonly TimeSpan _period = TimeSpan.FromSeconds(30);

    public ExpiredHoldSweeper(IServiceProvider serviceProvider, ILogger<ExpiredHoldSweeper> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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

    private async Task SweepExpiredHoldsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();

        var utcNow = DateTimeOffset.UtcNow;

        var expiredBookings = await db.SeatBookings
            .Where(b => b.Status == SeatBookingStatus.Held && b.HoldExpiry.HasValue && b.HoldExpiry.Value < utcNow)
            .ToListAsync(cancellationToken);

        if (expiredBookings.Count > 0)
        {
            _logger.LogInformation("Sweeper found {Count} expired held seat bookings. Transitioning to EXPIRED.", expiredBookings.Count);

            foreach (var booking in expiredBookings)
            {
                try
                {
                    booking.Expire();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to transition booking '{BookingId}' to EXPIRED.", booking.Id);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
