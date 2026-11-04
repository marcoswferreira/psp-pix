using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;

namespace Application.UseCases.Webhooks;

public class DispatchWebhookUseCase(IWebhookRepository repository, IWebhookSenderClient client)
{
    private readonly IWebhookRepository _repository = repository;
    private readonly IWebhookSenderClient _client = client;

    public async Task<bool> ExecuteDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        try
        {
            var (statusCode, error) = await _client.SendAsync(delivery.TargetUrl, delivery.PayloadJson, delivery.Signature, ct);

            if (statusCode is >= 200 and < 300)
            {
                delivery.RecordSuccess(statusCode);
                await _repository.UpdateDeliveryAsync(delivery, ct);
                return true;
            }

            delivery.RecordFailure(statusCode, error ?? $"HTTP {statusCode}");
            await _repository.UpdateDeliveryAsync(delivery, ct);
            return false;
        }
        catch (Exception ex)
        {
            delivery.RecordFailure(null, ex.Message);
            await _repository.UpdateDeliveryAsync(delivery, ct);
            return false;
        }
    }
}

