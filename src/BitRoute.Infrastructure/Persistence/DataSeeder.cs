using BitRoute.Domain.Entities;
using BitRoute.Domain.ValueObjects;
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

        // 1. Create Vehicles & Seats.
        // Diverse cabin layouts for different travel options:
        // a 2+2 coach (columns 1,2 | aisle 3 | 4,5), a 2+1 van, and a sprinter shuttle.
        var coachLayout = new VehicleLayout(rowCount: 4, seatsPerRow: 5, aisleAfterColumn: 2);
        var coach = Vehicle.Create("Luxury Coach 1", coachLayout, BuildSeats(coachLayout));

        var vanLayout = new VehicleLayout(rowCount: 2, seatsPerRow: 4, aisleAfterColumn: 2);
        var van = Vehicle.Create("Mini Van 1", vanLayout, BuildSeats(vanLayout));

        var shuttleLayout = new VehicleLayout(rowCount: 3, seatsPerRow: 4, aisleAfterColumn: 2);
        var shuttle = Vehicle.Create("Express Sprinter 1", shuttleLayout, BuildSeats(shuttleLayout));

        db.Vehicles.AddRange(coach, van, shuttle);

        // 2. Create Routes & Stops
        var route1 = Route.Create("Lagos to Ibadan", new[] { "Lagos", "Mowe", "Shagamu", "Ogere", "Ibadan" });
        var route2 = Route.Create("Ibadan to Abuja", new[] { "Ibadan", "Oyo", "Ogbomoso", "Ilorin", "Lokoja", "Abuja" });
        var route3 = Route.Create("Lagos to Benin City", new[] { "Lagos", "Ijebu Ode", "Ore", "Benin City" });
        var route4 = Route.Create("Abuja to Port Harcourt", new[] { "Abuja", "Lokoja", "Benin City", "Warri", "Port Harcourt" });

        db.Routes.AddRange(route1, route2, route3, route4);

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

        var legs3 = new[]
        {
            ScheduleLeg.Create(0, 1, 3500), // Lagos to Ore
            ScheduleLeg.Create(1, 2, 4500)  // Ore to Benin City
        };
        var schedule3 = Schedule.Create(route3.Id, shuttle.Id, new TimeOnly(7, 15, 0), legs3);

        var legs4 = new[]
        {
            ScheduleLeg.Create(0, 1, 3000), // Abuja to Lokoja
            ScheduleLeg.Create(1, 2, 5000), // Lokoja to Benin
            ScheduleLeg.Create(2, 3, 4000), // Benin to Warri
            ScheduleLeg.Create(3, 4, 3500)  // Warri to Port Harcourt
        };
        var schedule4 = Schedule.Create(route4.Id, coach.Id, new TimeOnly(6, 45, 0), legs4);

        db.Schedules.AddRange(schedule1, schedule2, schedule3, schedule4);
        await db.SaveChangesAsync();

        logger.LogInformation("Relational database seeding completed successfully.");
    }

    /// <summary>
    /// Fills a cabin row by row, skipping the aisle column. Labels follow the row-number +
    /// seat-letter convention the passenger reads on a ticket, and the letter counts only the
    /// seats in the row: on a 2+2 coach, row 1 is 1A, 1B, 1C, 1D at columns 1, 2, 4, 5.
    /// </summary>
    private static List<SeatPlacement> BuildSeats(VehicleLayout layout)
    {
        var placements = new List<SeatPlacement>();

        for (var row = 1; row <= layout.RowCount; row++)
        {
            var letterIndex = 0;

            for (var column = 1; column <= layout.SeatsPerRow; column++)
            {
                if (column == layout.AisleColumn) continue;

                placements.Add(new SeatPlacement($"{row}{(char)('A' + letterIndex)}", row, column));
                letterIndex++;
            }
        }

        return placements;
    }
}
