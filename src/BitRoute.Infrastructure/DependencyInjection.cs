using BitRoute.Infrastructure.Identity;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BitRoute.Infrastructure;

/// <summary>
/// Registers Infrastructure services with the DI container. Called from the Api composition
/// root so concrete implementations stay out of Domain and Application.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "Connection string 'Postgres' is not configured. Set ConnectionStrings__Postgres.");

        services.AddDbContext<BitRouteDbContext>(options => options.UseNpgsql(connectionString));

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<BitRouteDbContext>();

        return services;
    }
}
