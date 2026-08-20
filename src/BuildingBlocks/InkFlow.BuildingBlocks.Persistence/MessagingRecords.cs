namespace InkFlow.BuildingBlocks.Persistence;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(Guid id, DateTimeOffset occurredAtUtc, string type, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        Id = id;
        OccurredAtUtc = occurredAtUtc;
        Type = type;
        Payload = payload;
    }

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    public void MarkProcessed(DateTimeOffset processedAtUtc)
    {
        ProcessedAtUtc = processedAtUtc;
        LastError = null;
    }

    public void MarkFailed(string error)
    {
        Attempts++;
        LastError = error.Length <= 4000 ? error : error[..4000];
    }
}

public sealed class InboxMessage
{
    private InboxMessage()
    {
    }

    public InboxMessage(Guid messageId, string consumer, DateTimeOffset processedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        MessageId = messageId;
        Consumer = consumer;
        ProcessedAtUtc = processedAtUtc;
    }

    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; } = string.Empty;
    public DateTimeOffset ProcessedAtUtc { get; private set; }
}
