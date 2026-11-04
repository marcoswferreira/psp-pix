using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MiniPspPix.Application.Charges;
using MiniPspPix.Application.Webhooks;
using MiniPspPix.Infrastructure.Messaging;

namespace MiniPspPix.Infrastructure.BackgroundWorkers;

public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

    public OutboxPublisherWorker(IServiceProvider serviceProvider, ILogger<OutboxPublisherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker iniciado.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var outboxRepo = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

                var pendingMessages = await outboxRepo.GetUnprocessedBatchAsync(batchSize: 20, stoppingToken);

                foreach (var message in pendingMessages)
                {
                    try
                    {
                        await publisher.PublishAsync(message.EventType, message.PayloadJson, stoppingToken);
                        await outboxRepo.MarkAsProcessedAsync(message.Id, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao publicar mensagem outbox {MessageId}", message.Id);
                        await outboxRepo.RecordFailureAsync(message.Id, ex.Message, stoppingToken);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro no ciclo do OutboxPublisherWorker.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}

public class WebhookDeliveryWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebhookDeliveryWorker> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(2);

    public WebhookDeliveryWorker(IServiceProvider serviceProvider, ILogger<WebhookDeliveryWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WebhookDeliveryWorker iniciado com retry exponencial e suporte a DLQ.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var webhookRepo = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
                var dispatcher = scope.ServiceProvider.GetRequiredService<DispatchWebhookUseCase>();

                var pendingDeliveries = await webhookRepo.GetPendingDeliveriesAsync(batchSize: 10, stoppingToken);

                foreach (var delivery in pendingDeliveries)
                {
                    var success = await dispatcher.ExecuteDeliveryAsync(delivery, stoppingToken);
                    if (!success && delivery.Status == Domain.Webhooks.WebhookDeliveryStatus.DeadLettered)
                    {
                        _logger.LogWarning("Webhook delivery {DeliveryId} esgotou 5 tentativas e foi roteado para DEAD LETTER.", delivery.Id);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Erro no ciclo do WebhookDeliveryWorker.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}

