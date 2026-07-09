using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BitRoute.Infrastructure.Persistence;

/// <summary>
/// Supplies a <see cref="BitRouteDbContext"/> to the EF Core tools at design time (for example
/// when running "dotnet ef migrations add"). Generating a migration does not open a connection,
/// so a placeholder connection string is fine when none is set. This also frees migration
/// commands from needing the Api host or a loaded .env file. Real runtime configuration still
/// comes from the Api host through AddInfrastructure.
/// </summary>
public sealed class BitRouteDbContextFactory : IDesignTimeDbContextFactory<BitRouteDbContext>
{
    public BitRouteDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres");
        var options = new DbContextOptionsBuilder<BitRouteDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new BitRouteDbContext(options);
    }
}
