using BitRoute.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BitRoute.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedBookingDataAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BitRouteDbContext>>();

        // If routes already exist, database is already seeded
        if (await db.Routes.AnyAsync())
        {
            return;
        }

        logger.LogInformation("Seeding initial relational route, vehicle, and schedule data...");

        // 1. Create Vehicles & Seats
        var coach = Vehicle.Create("Luxury Coach 1", new[] { "1A", "1B", "2A", "2B", "3A", "3B", "4A", "4B" });
        var van = Vehicle.Create("Mini Van 1", new[] { "S1", "S2", "S3", "S4" });

        db.Vehicles.AddRange(coach, van);

        // 2. Create Routes & Stops
        var route1 = Route.Create("Lagos to Ibadan", new[] { "Lagos", "Shagamu", "Ibadan" });
        var route2 = Route.Create("Ibadan to Abuja", new[] { "Ibadan", "Ilorin", "Lokoja", "Abuja" });

        db.Routes.AddRange(route1, route2);

        // Save vehicles and routes first to establish foreign keys
        await db.SaveChangesAsync();

        // 3. Create Schedules & Legs
        var legs1 = new[]
        {
            ScheduleLeg.Create(0, 1, 1500), // Lagos to Shagamu
            ScheduleLeg.Create(1, 2, 2000)  // Shagamu to Ibadan
        };
        var schedule1 = Schedule.Create(route1.Id, coach.Id, new TimeOnly(8, 0, 0), legs1);

        var legs2 = new[]
        {
            ScheduleLeg.Create(0, 1, 2500), // Ibadan to Ilorin
            ScheduleLeg.Create(1, 2, 3000), // Ilorin to Lokoja
            ScheduleLeg.Create(2, 3, 4000)  // Lokoja to Abuja
        };
        var schedule2 = Schedule.Create(route2.Id, van.Id, new TimeOnly(9, 30, 0), legs2);

        db.Schedules.AddRange(schedule1, schedule2);
        await db.SaveChangesAsync();

        logger.LogInformation("Relational database seeding completed successfully.");
    }
}
