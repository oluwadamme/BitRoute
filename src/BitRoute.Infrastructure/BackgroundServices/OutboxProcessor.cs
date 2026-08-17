using System.Collections.Concurrent;
using System.Text.Json;
using BitRoute.Domain.Entities;
using BitRoute.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BitRoute.Infrastructure.BackgroundServices;

/// <summary>
/// Production-grade background worker that dispatches outbox domain events asynchronously via MediatR.
/// Supports type resolution caching, exponential backoff retries, and dead-letter thresholds.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);
    private static readonly ConcurrentDictionary<string, Type?> TypeCache = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _clock;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        IServiceScopeFactory scopeFactory,
        TimeProvider clock,
        ILogger<OutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor background service started.");

        using var timer = new PeriodicTimer(PollingInterval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while executing OutboxProcessor batch cycle.");
            }
        }
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BitRouteDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var now = _clock.GetUtcNow();

        // Query unhandled messages whose next attempt instant has arrived or was never set
        var candidateMessages = await dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .Take(50)
            .ToListAsync(cancellationToken);

        var messages = candidateMessages
            .Where(m => m.NextAttemptUtc == null || m.NextAttemptUtc.Value <= now)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(20)
            .ToList();

        if (messages.Count == 0) return;

        _logger.LogInformation("Processing batch of {Count} outbox message(s).", messages.Count);

        foreach (var message in messages)
        {
            try
            {
                var eventType = ResolveType(message.Type);
                if (eventType is null)
                {
                    throw new InvalidOperationException($"Could not resolve type '{message.Type}' for OutboxMessage '{message.Id}'.");
                }

                var domainEvent = JsonSerializer.Deserialize(message.Content, eventType);
                if (domainEvent is null)
                {
                    throw new InvalidOperationException($"Failed to deserialize content for OutboxMessage '{message.Id}' into type '{eventType.FullName}'.");
                }

                if (domainEvent is INotification notification)
                {
                    await publisher.Publish(notification, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("OutboxMessage '{Id}' of type '{Type}' does not implement INotification.", message.Id, message.Type);
                }

                message.MarkProcessed(now);
                _logger.LogInformation("Successfully dispatched Outbox Event [{Type}] ID: '{Id}'.", message.Type, message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox message '{Id}' (Type: '{Type}', Attempt {Attempt}/{MaxRetries}).",
                    message.Id, message.Type, message.RetryCount + 1, OutboxMessage.MaxRetries);

                if (message.RetryCount + 1 >= OutboxMessage.MaxRetries)
                {
                    message.RecordDeadLetter(ex.Message);
                    _logger.LogError("OutboxMessage '{Id}' reached maximum retry attempts ({MaxRetries}) and was marked as DEAD-LETTER.",
                        message.Id, OutboxMessage.MaxRetries);
                }
                else
                {
                    // Exponential backoff: 5s, 10s, 20s, 40s...
                    var backoffSeconds = Math.Pow(2, message.RetryCount) * 5;
                    var nextAttempt = now.AddSeconds(backoffSeconds);
                    message.RecordFailure(ex.Message, nextAttempt);

                    _logger.LogWarning("Scheduled retry #{RetryCount} for OutboxMessage '{Id}' at {NextAttemptUtc}.",
                        message.RetryCount, message.Id, nextAttempt);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Type? ResolveType(string typeName)
    {
        return TypeCache.GetOrAdd(typeName, name =>
        {
            var type = Type.GetType(name);
            if (type is not null) return type;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = assembly.GetType(name);
                if (type is not null) return type;
            }

            return null;
        });
    }
}
