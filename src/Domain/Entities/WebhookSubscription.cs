using Domain.Common.Entities;

namespace Domain.Entities;

public class WebhookSubscription : BaseEntity, IAggregateRoot
{
    public string MerchantId { get; private set; }
    public string TargetUrl { get; private set; }
    public string SecretKey { get; private set; }
    public bool IsActive { get; private set; }

    protected WebhookSubscription()
    {
        MerchantId = null!;
        TargetUrl = null!;
        SecretKey = null!;
    }

    public WebhookSubscription(string merchantId, string targetUrl, string? secretKey = null)
    {
        Id = Guid.NewGuid();
        MerchantId = merchantId;
        TargetUrl = targetUrl;
        SecretKey = secretKey ?? Guid.NewGuid().ToString("N");
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

