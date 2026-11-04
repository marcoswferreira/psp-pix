using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IChargeRepository
{
    Task AddAsync(PixCharge charge, CancellationToken ct = default);
    Task<PixCharge?> GetByTxIdAsync(string txId, CancellationToken ct = default);
    Task<PixCharge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(PixCharge charge, CancellationToken ct = default);
}

public interface ILedgerRepository
{
    Task<LedgerAccount?> GetAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(Guid accountId, CancellationToken ct = default);
    Task<LedgerTransaction> RecordDoubleEntryTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default);

    Task UnsafeTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 20, CancellationToken ct = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task RecordFailureAsync(Guid messageId, string error, CancellationToken ct = default);
}

public interface IWebhookRepository
{
    Task AddSubscriptionAsync(WebhookSubscription subscription, CancellationToken ct = default);
    Task<WebhookSubscription?> GetSubscriptionByMerchantIdAsync(string merchantId, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default);
    Task AddDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task UpdateDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> GetPendingDeliveriesAsync(int batchSize = 10, CancellationToken ct = default);
}

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(string key, string requestHash, string route, TimeSpan lockDuration, CancellationToken ct = default);
    Task SaveResultAsync(string key, int statusCode, string responseBody, CancellationToken ct = default);
}

