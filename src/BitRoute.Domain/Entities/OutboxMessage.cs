namespace BitRoute.Domain.Entities;

/// <summary>
/// Domain entity representing an outbox message written in the same transaction
/// as a state change and dispatched asynchronously by a background worker.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset? ProcessedOnUtc { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }
    public DateTimeOffset? NextAttemptUtc { get; private set; }

    public const int MaxRetries = 5;

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string content, DateTimeOffset occurredOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOnUtc = occurredOnUtc,
            Type = type,
            Content = content,
            ProcessedOnUtc = null,
            Error = null,
            RetryCount = 0,
            NextAttemptUtc = null
        };
    }

    public void MarkProcessed(DateTimeOffset processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        Error = null;
        NextAttemptUtc = null;
    }

    public void RecordFailure(string error, DateTimeOffset nextAttemptUtc)
    {
        RetryCount++;
        Error = error;
        NextAttemptUtc = nextAttemptUtc;
    }

    public void RecordDeadLetter(string error)
    {
        RetryCount++;
        Error = $"[DEAD-LETTER] Max retries ({MaxRetries}) exceeded: {error}";
        NextAttemptUtc = null;
    }
}
