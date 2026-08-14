using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace BitRoute.Api.Hubs;

public sealed class TelemetryHub : Hub
{
    private readonly ILogger<TelemetryHub> _logger;

    public TelemetryHub(ILogger<TelemetryHub> logger)
    {
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
        var telemetryPayload = new
        {
            ScheduleId = scheduleId,
            Latitude = latitude,
            Longitude = longitude,
            CurrentLegIndex = currentLegIndex,
            Timestamp = DateTimeOffset.UtcNow
        };

        await Clients.Group(groupName).SendAsync("ReceiveVehicleLocation", telemetryPayload);
        _logger.LogDebug("Broadcasted driver location for schedule '{ScheduleId}' to group '{GroupName}'.", scheduleId, groupName);
    }

    private static string GetGroupName(Guid scheduleId) => $"Schedule_{scheduleId}";
}
