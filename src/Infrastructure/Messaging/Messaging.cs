using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default);
}

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

public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly string _exchangeName;
    private bool _disposed;

    public RabbitMqEventPublisher(IConfiguration configuration, ILogger<RabbitMqEventPublisher> logger)
    {
        _logger = logger;
        _exchangeName = configuration["RabbitMQ:Exchange"] ?? "pix.events";

        try
        {
            var host = configuration["RabbitMQ:Host"] ?? "localhost";
            var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5672;
            var user = configuration["RabbitMQ:User"] ?? "guest";
            var pass = configuration["RabbitMQ:Password"] ?? "guest";

            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = user,
                Password = pass,
                DispatchConsumersAsync = true,
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(15)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Topic, durable: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ connection could not be established. Falling back to no-op logging mode.");
            _channel = null;
            _connection = null;
        }
    }

    public Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        if (_channel == null || _channel.IsClosed)
        {
            _logger.LogInformation("[RabbitMQ Fallback] Event {EventType}: {Payload}", eventType, payloadJson);
            return Task.CompletedTask;
        }

        try
        {
            var body = Encoding.UTF8.GetBytes(payloadJson);
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Type = eventType;
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            _channel.BasicPublish(
                exchange: _exchangeName,
                routingKey: eventType,
                basicProperties: properties,
                body: body
            );

            _logger.LogInformation("Published event {EventType} to exchange {Exchange}", eventType, _exchangeName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish event {EventType} to RabbitMQ", eventType);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _channel?.Close(); _channel?.Dispose(); } catch { }
        try { _connection?.Close(); _connection?.Dispose(); } catch { }
    }
}

