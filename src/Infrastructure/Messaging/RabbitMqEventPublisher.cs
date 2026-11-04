using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to RabbitMQ.");
            throw;
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _channel?.Dispose();
        _connection?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

