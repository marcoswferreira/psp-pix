using MiniPspPix.Domain.Webhooks;

namespace MiniPspPix.Application.Webhooks;

public record RegisterWebhookCommand(
    string MerchantId,
    string TargetUrl,
    string? SecretKey = null
);

public interface IWebhookRepository
{
    Task AddSubscriptionAsync(WebhookSubscription subscription, CancellationToken ct = default);
    Task<WebhookSubscription?> GetSubscriptionByMerchantIdAsync(string merchantId, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default);
    Task AddDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task UpdateDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> GetPendingDeliveriesAsync(int batchSize = 10, CancellationToken ct = default);
}

public interface IWebhookSenderClient
{
    Task<(int StatusCode, string? Error)> SendAsync(string url, string payloadJson, string signature, CancellationToken ct = default);
}

public class DispatchWebhookUseCase
{
    private readonly IWebhookRepository _repository;
    private readonly IWebhookSenderClient _client;

    public DispatchWebhookUseCase(IWebhookRepository repository, IWebhookSenderClient client)
    {
        _repository = repository;
        _client = client;
    }

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

