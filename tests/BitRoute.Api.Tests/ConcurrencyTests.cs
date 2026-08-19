using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using BitRoute.Application;
using BitRoute.Application.Booking;
using BitRoute.Domain.Entities;
using BitRoute.Domain.Enums;
using BitRoute.Domain.Interfaces;
using BitRoute.Domain.ValueObjects;
using BitRoute.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace BitRoute.Api.Tests;

[Collection("IntegrationTests")]
public class ConcurrencyTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly string _dbFile;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();

    private Guid _scheduleId;
    private Guid _seatId;
    private DateOnly _travelDate;

    public ConcurrencyTests(WebApplicationFactory<Program> factory)
    {
        _dbFile = $"test_concurrency_{Guid.NewGuid():N}.db";

        // Set environment variables before host builds
        Environment.SetEnvironmentVariable("ConnectionStrings__Postgres", $"Data Source={_dbFile}");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "bitroute");
        Environment.SetEnvironmentVariable("Jwt__Audience", "bitroute-clients");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "test-signing-key-that-is-long-enough-123456");
        Environment.SetEnvironmentVariable("Jwt__AccessTokenMinutes", "60");
        Environment.SetEnvironmentVariable("Jwt__RefreshTokenDays", "30");
        Environment.SetEnvironmentVariable("Paystack__SecretKey", "sk_test_dummy_for_options_validation");
        Environment.SetEnvironmentVariable("Paystack__PublicKey", "pk_test_dummy_for_options_validation");
        Environment.SetEnvironmentVariable("Paystack__CallbackUrl", "http://localhost:3001/payment/callback");

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                // Replace Redis with a mock
                services.RemoveAll<IConnectionMultiplexer>();
                services.AddSingleton<IConnectionMultiplexer>(_redisMock.Object);
            });
        });

        SeedDatabase();
    }

    private void SeedDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();
        
        // Ensure database schema is created
        db.Database.EnsureCreated();

        var route = Route.Create("Lagos to Ibadan", new[] { "Lagos", "Shagamu", "Ibadan" });

        // A single row of two seats, no aisle. The seat plan is irrelevant to this test; what
        // matters is that Seat1 is the one seat every request fights over.
        var vehicle = Vehicle.Create(
            "Luxury Coach",
            new VehicleLayout(rowCount: 1, seatsPerRow: 2, aisleAfterColumn: null),
            new[]
            {
                new SeatPlacement("Seat1", 1, 1),
                new SeatPlacement("Seat2", 1, 2)
            });

        db.Routes.Add(route);
        db.Vehicles.Add(vehicle);
        db.SaveChanges();

        var legs = new[]
        {
            ScheduleLeg.Create(0, 1, 1000),
            ScheduleLeg.Create(1, 2, 1200)
        };
        var schedule = Schedule.Create(route.Id, vehicle.Id, new TimeOnly(8, 0), legs);
        db.Schedules.Add(schedule);
        db.SaveChanges();

        _scheduleId = schedule.Id;
        _seatId = vehicle.Seats.First().Id;
        _travelDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
    }

    public void Dispose()
    {
        _factory.Dispose();
        try
        {
            if (File.Exists(_dbFile)) File.Delete(_dbFile);
        }
        catch { }
    }

    private HttpClient CreateAuthenticatedClient(Guid userId)
    {
        var tokenGenerator = _factory.Services.GetRequiredService<ITokenGenerator>();
        var accessToken = tokenGenerator.GenerateAccessToken(userId, $"{userId}@bitroute.com", [UserRole.Passenger]);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
        return client;
    }

    [Fact]
    public async Task HoldSeat_ParallelConcurrentRequests_OnlyOneSucceeds()
    {
        // Define N concurrent users attempting to book overlapping legs on the same seat
        const int concurrentRequestCount = 5;
        
        var clients = Enumerable.Range(0, concurrentRequestCount)
            .Select(_ => CreateAuthenticatedClient(Guid.NewGuid()))
            .ToList();

        // All users request the overlapping segment [0, 2)
        var requests = Enumerable.Range(0, concurrentRequestCount)
            .Select(i => new HoldSeatRequest(Guid.NewGuid(), _scheduleId, _travelDate, _seatId, 0, 2, $"idempotency-{i}"))
            .ToList();

        // Fire all requests concurrently
        var tasks = clients.Select((client, index) => 
            client.PostAsJsonAsync("/bookings/hold", requests[index]))
            .ToList();

        var responses = await Task.WhenAll(tasks);

        // Assert that exactly one request succeeded (returns 200 OK)
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        
        // Assert that all others failed with 409 Conflict (serializable concurrency conflict)
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        Assert.Equal(1, successCount);
        Assert.Equal(concurrentRequestCount - 1, conflictCount);
    }
}
