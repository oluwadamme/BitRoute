using BitRoute.Domain.Entities;
using BitRoute.Infrastructure.Caching;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BitRoute.Api.Hubs;

public sealed class TelemetryHub : Hub
{
    private readonly ICacheService _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(
        ICacheService cache,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryHub> logger)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task JoinScheduleGroup(Guid scheduleId)
    {
        var groupName = GetGroupName(scheduleId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client '{ConnectionId}' joined telemetry group '{GroupName}'.", Context.ConnectionId, groupName);
    }

    public async Task LeaveScheduleGroup(Guid scheduleId)
    {
        var groupName = GetGroupName(scheduleId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        _logger.LogInformation("Client '{ConnectionId}' left telemetry group '{GroupName}'.", Context.ConnectionId, groupName);
    }

    public async Task SendDriverLocation(Guid scheduleId, double latitude, double longitude, int currentLegIndex)
    {
        var groupName = GetGroupName(scheduleId);
        var timestamp = DateTimeOffset.UtcNow;

        var telemetryPayload = new
        {
            ScheduleId = scheduleId,
            Latitude = latitude,
            Longitude = longitude,
            CurrentLegIndex = currentLegIndex,
            Timestamp = timestamp
        };

        // 1. Broadcast over SignalR in real time
        await Clients.Group(groupName).SendAsync("ReceiveVehicleLocation", telemetryPayload);

        // 2. Cache latest telemetry location in Redis
        var redisKey = $"telemetry:latest:{scheduleId}";
        await _cache.SetAsync(redisKey, telemetryPayload, TimeSpan.FromHours(24));
        await _cache.SetGeoLocationAsync("telemetry:active_vehicles", scheduleId.ToString(), longitude, latitude);

        // 3. Persist history log to PostgreSQL database using a scoped DbContext
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();
            var log = VehicleTelemetryLog.Create(scheduleId, latitude, longitude, currentLegIndex, timestamp);
            db.VehicleTelemetryLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log telemetry to database for schedule '{ScheduleId}'.", scheduleId);
        }

        _logger.LogDebug("Broadcasted and persisted driver location for schedule '{ScheduleId}' to group '{GroupName}'.", scheduleId, groupName);
    }

    private static string GetGroupName(Guid scheduleId) => $"Schedule_{scheduleId}";
}
