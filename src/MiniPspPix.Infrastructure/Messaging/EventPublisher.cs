using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace MiniPspPix.Infrastructure.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default);
}

public class RabbitMqEventPublisher : IEventPublisher, IDisposable
{
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly string _connectionString;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public const string ExchangeName = "pix.events";
    public const string DeadLetterExchange = "dlx.pix";
    public const string DeadLetterQueue = "pix.webhook.dlq";
    public const string WebhookQueue = "pix.webhook.deliveries";

    public RabbitMqEventPublisher(string connectionString, ILogger<RabbitMqEventPublisher> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        try
        {
            EnsureConnected();
            if (_channel == null)
            {
                _logger.LogWarning("RabbitMQ channel indisponível. Mensagem outbox retida no banco para retry posterior.");
                return Task.CompletedTask;
            }

            var body = Encoding.UTF8.GetBytes(payloadJson);
            var props = _channel.CreateBasicProperties();
            props.DeliveryMode = 2; // Persistent
            props.ContentType = "application/json";
            props.Type = eventType;
            props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            lock (_lock)
            {
                _channel.BasicPublish(
                    exchange: ExchangeName,
                    routingKey: eventType,
                    basicProperties: props,
                    body: body
                );
            }

            _logger.LogInformation("Evento {EventType} publicado com sucesso no RabbitMQ.", eventType);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao publicar evento {EventType} no RabbitMQ. A mensagem permanecerá pendente na tabela Outbox.", eventType);
        }

        return Task.CompletedTask;
    }

    private void EnsureConnected()
    {
        if (_channel is { IsOpen: true }) return;

        lock (_lock)
        {
            if (_channel is { IsOpen: true }) return;

            try
            {
                var factory = new ConnectionFactory
                {
                    Uri = new Uri(_connectionString),
                    DispatchConsumersAsync = true,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
                };

                _connection = factory.CreateConnection();
                _channel = _connection.CreateModel();

                // Topologia com Dead Letter Exchange (DLQ)
                _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true);
                _channel.ExchangeDeclare(DeadLetterExchange, ExchangeType.Direct, durable: true);

                // Dead Letter Queue
                _channel.QueueDeclare(
                    queue: DeadLetterQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false
                );
                _channel.QueueBind(DeadLetterQueue, DeadLetterExchange, "dead-letter");

                // Fila principal com DLX configurado
                var queueArgs = new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", DeadLetterExchange },
                    { "x-dead-letter-routing-key", "dead-letter" }
                };

                _channel.QueueDeclare(
                    queue: WebhookQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: queueArgs
                );

                _channel.QueueBind(WebhookQueue, ExchangeName, "pix.#");

                _logger.LogInformation("Topologia do RabbitMQ declarada com sucesso (Exchange: {Exchange}, DLQ: {DLQ}).", ExchangeName, DeadLetterQueue);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Não foi possível conectar ao RabbitMQ ({Message}). O Outbox Worker continuará retendo os eventos até restabelecimento.", ex.Message);
            }
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}

public class InMemoryEventPublisher : IEventPublisher
{
    private readonly ILogger<InMemoryEventPublisher> _logger;
    public List<(string EventType, string Payload)> PublishedEvents { get; } = new();

    public InMemoryEventPublisher(ILogger<InMemoryEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(string eventType, string payloadJson, CancellationToken ct = default)
    {
        PublishedEvents.Add((eventType, payloadJson));
        _logger.LogInformation("[InMemory Broker] Evento {EventType} publicado.", eventType);
        return Task.CompletedTask;
    }
}

