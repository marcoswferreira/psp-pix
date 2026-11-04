using Microsoft.EntityFrameworkCore;
using MiniPspPix.Application.Charges;
using MiniPspPix.Application.Idempotency;
using MiniPspPix.Application.Webhooks;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Idempotency;
using MiniPspPix.Domain.Outbox;
using MiniPspPix.Domain.Webhooks;

namespace MiniPspPix.Infrastructure.Persistence;

public class EfCoreChargeRepository : IChargeRepository
{
    private readonly PixDbContext _db;

    public EfCoreChargeRepository(PixDbContext db) => _db = db;

    public async Task AddAsync(PixCharge charge, CancellationToken ct = default)
    {
        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PixCharge?> GetByTxIdAsync(string txId, CancellationToken ct = default)
    {
        return await _db.Charges.FirstOrDefaultAsync(c => c.TxId == txId, ct);
    }

    public async Task<PixCharge?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Charges.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(PixCharge charge, CancellationToken ct = default)
    {
        _db.Charges.Update(charge);
        await _db.SaveChangesAsync(ct);
    }
}

public class EfCoreOutboxRepository : IOutboxRepository
{
    private readonly PixDbContext _db;

    public EfCoreOutboxRepository(PixDbContext db) => _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        return await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (msg != null)
        {
            msg.MarkAsProcessed();
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RecordFailureAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (msg != null)
        {
            msg.RecordFailure(error);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class EfCoreWebhookRepository : IWebhookRepository
{
    private readonly PixDbContext _db;

    public EfCoreWebhookRepository(PixDbContext db) => _db = db;

    public async Task AddSubscriptionAsync(WebhookSubscription subscription, CancellationToken ct = default)
    {
        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<WebhookSubscription?> GetSubscriptionByMerchantIdAsync(string merchantId, CancellationToken ct = default)
    {
        return await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.MerchantId == merchantId && s.IsActive, ct);
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default)
    {
        return await _db.WebhookSubscriptions.Where(s => s.IsActive).ToListAsync(ct);
    }

    public async Task AddDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _db.WebhookDeliveries.Update(delivery);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetPendingDeliveriesAsync(int batchSize = 10, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.WebhookDeliveries
            .Where(d => (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Failed)
                        && (d.NextRetryAtUtc == null || d.NextRetryAtUtc <= now))
            .OrderBy(d => d.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}

public class EfCoreIdempotencyStore : IIdempotencyStore
{
    private readonly PixDbContext _db;

    public EfCoreIdempotencyStore(PixDbContext db) => _db = db;

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        return await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);
    }

    public async Task<bool> TryAcquireLockAsync(string key, string requestHash, string route, TimeSpan lockDuration, CancellationToken ct = default)
    {
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);

        if (existing == null)
        {
            var record = new IdempotencyRecord(key, requestHash, route, lockDuration);
            _db.IdempotencyRecords.Add(record);
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                // Colisão de chave por corrida paralela
                return false;
            }
        }

        // Se o lock expirou, permite readquirir
        if (existing.Status == IdempotencyStatus.InProgress && existing.HasLockExpired())
        {
            _db.IdempotencyRecords.Remove(existing);
            await _db.SaveChangesAsync(ct);

            var newRecord = new IdempotencyRecord(key, requestHash, route, lockDuration);
            _db.IdempotencyRecords.Add(newRecord);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        return false;
    }

    public async Task SaveResultAsync(string key, int statusCode, string responseBody, CancellationToken ct = default)
    {
        var record = await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);
        if (record != null)
        {
            record.Complete(statusCode, responseBody);
            await _db.SaveChangesAsync(ct);
        }
    }
}

