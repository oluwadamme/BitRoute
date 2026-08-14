using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace BitRoute.Infrastructure.Health;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_redis.IsConnected)
            {
                return HealthCheckResult.Unhealthy("Redis connection is down.");
            }

            var latency = await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Redis connection is healthy. Ping: {latency.TotalMilliseconds:F1}ms");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
        }
    }
}
