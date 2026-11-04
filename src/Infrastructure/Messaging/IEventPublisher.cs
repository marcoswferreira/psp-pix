namespace Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default);
}

