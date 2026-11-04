using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class WebhookRepository(ApplicationDbContext db) : IWebhookRepository
{
    private readonly ApplicationDbContext _db = db;

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

