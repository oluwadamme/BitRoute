using BitRoute.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BitRoute.Infrastructure.Health;

public sealed class PostgresHealthCheck : IHealthCheck
{
    private readonly BitRouteDbContext _dbContext;

    public PostgresHealthCheck(BitRouteDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("PostgreSQL connection is healthy.");
            }
            return HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL database.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL health check failed.", ex);
        }
    }
}
