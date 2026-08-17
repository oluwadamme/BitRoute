using System.Text.Json;
using BitRoute.Application.Booking.Events;
using BitRoute.Domain.Entities;
using BitRoute.Infrastructure.BackgroundServices;
using BitRoute.Infrastructure.Persistence;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace BitRoute.Infrastructure.Tests.BackgroundServices;

public sealed class OutboxProcessorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<BitRouteDbContext> _options;
    private readonly TestTimeProvider _clock;

    public OutboxProcessorTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<BitRouteDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var db = new BitRouteDbContext(_options);
        db.Database.EnsureCreated();

        _clock = new TestTimeProvider(new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero));
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private sealed class TestPublisher : IPublisher
    {
        public readonly List<object> PublishedEvents = new();
        public bool ShouldThrow { get; set; }

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated MediatR handler failure.");
            }

            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            return Publish((object)notification, cancellationToken);
        }
    }

    private (ServiceProvider ScopeFactoryProvider, TestPublisher Publisher) BuildDependencies(BitRouteDbContext db)
    {
        var publisher = new TestPublisher();
        var services = new ServiceCollection();
        services.AddScoped(_ => db);
        services.AddSingleton<IPublisher>(publisher);

        return (services.BuildServiceProvider(), publisher);
    }

    [Fact]
    public async Task ProcessBatchAsync_DispatchesEvent_AndMarksProcessed()
    {
        // Arrange
        using var db = new BitRouteDbContext(_options);
        var expiredEvent = new SeatHoldExpiredEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 8, 20), Guid.NewGuid(), 0, 1, _clock.GetUtcNow());

        var message = OutboxMessage.Create(
            typeof(SeatHoldExpiredEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(expiredEvent),
            _clock.GetUtcNow());

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var (serviceProvider, publisher) = BuildDependencies(db);
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var processor = new OutboxProcessor(scopeFactory, _clock, NullLogger<OutboxProcessor>.Instance);

        // Act
        await processor.ProcessBatchAsync();

        // Assert
        using var verifyDb = new BitRouteDbContext(_options);
        var reloadedMessage = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(reloadedMessage);
        Assert.NotNull(reloadedMessage.ProcessedOnUtc);
        Assert.Equal(_clock.GetUtcNow(), reloadedMessage.ProcessedOnUtc.Value);
        Assert.Single(publisher.PublishedEvents);

        var dispatched = Assert.IsType<SeatHoldExpiredEvent>(publisher.PublishedEvents[0]);
        Assert.Equal(expiredEvent.BookingId, dispatched.BookingId);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenHandlerThrows_RecordsExponentialBackoffRetry()
    {
        // Arrange
        using var db = new BitRouteDbContext(_options);
        var expiredEvent = new SeatHoldExpiredEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 8, 20), Guid.NewGuid(), 0, 1, _clock.GetUtcNow());

        var message = OutboxMessage.Create(
            typeof(SeatHoldExpiredEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(expiredEvent),
            _clock.GetUtcNow());

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var (serviceProvider, publisher) = BuildDependencies(db);
        publisher.ShouldThrow = true;

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var processor = new OutboxProcessor(scopeFactory, _clock, NullLogger<OutboxProcessor>.Instance);

        // Act
        await processor.ProcessBatchAsync();

        // Assert
        using var verifyDb = new BitRouteDbContext(_options);
        var reloadedMessage = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(reloadedMessage);
        Assert.Null(reloadedMessage.ProcessedOnUtc);
        Assert.Equal(1, reloadedMessage.RetryCount);
        Assert.NotNull(reloadedMessage.NextAttemptUtc);
        Assert.Equal(_clock.GetUtcNow().AddSeconds(5), reloadedMessage.NextAttemptUtc.Value); // 2^0 * 5s = 5s
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenMaxRetriesExceeded_RecordsDeadLetter()
    {
        // Arrange
        using var db = new BitRouteDbContext(_options);
        var expiredEvent = new SeatHoldExpiredEvent(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 8, 20), Guid.NewGuid(), 0, 1, _clock.GetUtcNow());

        var message = OutboxMessage.Create(
            typeof(SeatHoldExpiredEvent).AssemblyQualifiedName!,
            JsonSerializer.Serialize(expiredEvent),
            _clock.GetUtcNow());

        // Simulate 4 prior failures
        for (int i = 0; i < 4; i++)
        {
            message.RecordFailure("Prior failure", _clock.GetUtcNow());
        }

        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync();

        var (serviceProvider, publisher) = BuildDependencies(db);
        publisher.ShouldThrow = true;

        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var processor = new OutboxProcessor(scopeFactory, _clock, NullLogger<OutboxProcessor>.Instance);

        // Act
        await processor.ProcessBatchAsync();

        // Assert
        using var verifyDb = new BitRouteDbContext(_options);
        var reloadedMessage = await verifyDb.OutboxMessages.FindAsync(message.Id);
        Assert.NotNull(reloadedMessage);
        Assert.Null(reloadedMessage.ProcessedOnUtc);
        Assert.Equal(5, reloadedMessage.RetryCount);
        Assert.NotNull(reloadedMessage.Error);
        Assert.StartsWith("[DEAD-LETTER]", reloadedMessage.Error);
    }
}
