using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

public class InMemoryEventPublisher : IEventPublisher
{
    private readonly ConcurrentBag<(string EventType, string Payload)> _publishedEvents = new();
    private readonly ILogger<InMemoryEventPublisher> _logger;

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger)
    {
        _logger = logger;
    }

    public IReadOnlyCollection<(string EventType, string Payload)> PublishedEvents => _publishedEvents;

    public Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        _publishedEvents.Add((eventType, payloadJson));
        _logger.LogInformation("[InMemoryEventPublisher] Published event {EventType}: {Payload}", eventType, payloadJson);
        return Task.CompletedTask;
    }
}

