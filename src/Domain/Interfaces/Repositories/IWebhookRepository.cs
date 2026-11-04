using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IWebhookRepository
{
    Task AddSubscriptionAsync(WebhookSubscription subscription, CancellationToken ct = default);
    Task<WebhookSubscription?> GetSubscriptionByMerchantIdAsync(string merchantId, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default);
    Task AddDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task UpdateDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> GetPendingDeliveriesAsync(int batchSize = 10, CancellationToken ct = default);
}

