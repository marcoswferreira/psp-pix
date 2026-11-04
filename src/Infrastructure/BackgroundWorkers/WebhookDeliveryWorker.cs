using Application.UseCases.Webhooks;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundWorkers;

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
        _logger.LogInformation("WebhookDeliveryWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var webhookRepo = scope.ServiceProvider.GetRequiredService<IWebhookRepository>();
                var dispatchUseCase = scope.ServiceProvider.GetRequiredService<DispatchWebhookUseCase>();

                var deliveries = await webhookRepo.GetPendingDeliveriesAsync(10, stoppingToken);

                foreach (var delivery in deliveries)
                {
                    try
                    {
                        var success = await dispatchUseCase.ExecuteDeliveryAsync(delivery, stoppingToken);
                        _logger.LogInformation("Webhook delivery {DeliveryId} result: Success={Success}", delivery.Id, success);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception delivering webhook {DeliveryId}", delivery.Id);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred in WebhookDeliveryWorker cycle.");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("WebhookDeliveryWorker stopped.");
    }
}

