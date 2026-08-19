using System.Text;
using BitRoute.Domain.Interfaces;
using BitRoute.Infrastructure.Identity;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using BitRoute.Infrastructure.Caching;
using BitRoute.Infrastructure.Persistence.Repositories;
using BitRoute.Application.Booking;
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

        if (connectionString.StartsWith("DataSource=") || connectionString.StartsWith("Data Source="))
        {
            services.AddDbContext<BitRouteDbContext>(options => options.UseSqlite(connectionString));
        }
        else
        {
            services.AddDbContext<BitRouteDbContext>(options => options.UseNpgsql(connectionString));
        }

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // Length-only policy, aligned with the FluentValidation rules and NIST
                // guidance (length beats composition rules). Keep both in sync.
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequireDigit = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireLowercase = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<BitRouteDbContext>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(
                o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32,
                "Jwt:SigningKey must be at least 32 bytes.")
            .Validate(o => o.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be positive.")
            .Validate(o => o.RefreshTokenDays > 0, "Jwt:RefreshTokenDays must be positive.")
            .ValidateOnStart();

        services.AddOptions<PaystackOptions>()
            .Bind(configuration.GetSection(PaystackOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.SecretKey), "Paystack:SecretKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.PublicKey), "Paystack:PublicKey is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.CallbackUrl), "Paystack:CallbackUrl is required.")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IIdentityService, IdentityService>();

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Connection string 'Redis' is not configured. Set ConnectionStrings__Redis.");
        services.AddSingleton<IConnectionMultiplexer>(
            _ => ConnectionMultiplexer.Connect(redisConnectionString));
        services.AddSingleton<Caching.ICacheService, Caching.RedisCacheService>();
        services.AddSingleton<IRefreshTokenCrypto, RefreshTokenCrypto>();
        services.AddSingleton<IRefreshTokenStore, RedisRefreshTokenStore>();

        services.AddOptions<Payments.PaystackOptions>()
            .Bind(configuration.GetSection(Payments.PaystackOptions.SectionName));

        services.AddHttpClient<IPaystackService, Payments.PaystackService>()
            .AddStandardResilienceHandler();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IRouteRepository, RouteRepository>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IScheduleRepository, ScheduleRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddSingleton<IAvailabilityCache, RedisAvailabilityCache>();
        services.AddSingleton<ITelemetryCache, RedisTelemetryCache>();

        services.AddHostedService<BackgroundServices.ExpiredHoldSweeper>();
        services.AddHostedService<BackgroundServices.OutboxProcessor>();

        services.AddHealthChecks()
            .AddCheck<Health.PostgresHealthCheck>("postgresql")
            .AddCheck<Health.RedisHealthCheck>("redis");

        return services;
    }
}
