using Domain.Common.Entities;

namespace Domain.Entities;

public class OutboxMessage : BaseEntity, IAggregateRoot
{
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    protected OutboxMessage()
    {
        EventType = null!;
        PayloadJson = null!;
    }

    public OutboxMessage(string eventType, string payloadJson)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        PayloadJson = payloadJson;
        CreatedAtUtc = DateTime.UtcNow;
        RetryCount = 0;
    }

    public void MarkAsProcessed()
    {
        ProcessedAtUtc = DateTime.UtcNow;
        LastError = null;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordFailure(string error)
    {
        RetryCount++;
        LastError = error;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

