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
            Error = null
        };
    }

    public void MarkProcessed(DateTimeOffset processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
    }

    public void MarkFailed(string error)
    {
        Error = error;
    }
}
