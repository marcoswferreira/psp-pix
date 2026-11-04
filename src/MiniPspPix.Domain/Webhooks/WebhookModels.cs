namespace MiniPspPix.Domain.Webhooks;

public enum WebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Failed = 3,
    DeadLettered = 4
}

public class WebhookSubscription
{
    public Guid Id { get; private set; }
    public string MerchantId { get; private set; }
    public string TargetUrl { get; private set; }
    public string SecretKey { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

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

    public void Deactivate() => IsActive = false;
}

public class WebhookDelivery
{
    public Guid Id { get; private set; }
    public Guid SubscriptionId { get; private set; }
    public string TargetUrl { get; private set; }
    public string EventType { get; private set; }
    public string PayloadJson { get; private set; }
    public string Signature { get; private set; }
    public WebhookDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime? NextRetryAtUtc { get; private set; }
    public int? LastHttpStatus { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? DeliveredAtUtc { get; private set; }

    protected WebhookDelivery()
    {
        TargetUrl = null!;
        EventType = null!;
        PayloadJson = null!;
        Signature = null!;
    }

    public WebhookDelivery(
        Guid subscriptionId,
        string targetUrl,
        string eventType,
        string payloadJson,
        string signature)
    {
        Id = Guid.NewGuid();
        SubscriptionId = subscriptionId;
        TargetUrl = targetUrl;
        EventType = eventType;
        PayloadJson = payloadJson;
        Signature = signature;
        Status = WebhookDeliveryStatus.Pending;
        AttemptCount = 0;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void RecordSuccess(int httpStatus)
    {
        AttemptCount++;
        LastHttpStatus = httpStatus;
        LastError = null;
        Status = WebhookDeliveryStatus.Delivered;
        DeliveredAtUtc = DateTime.UtcNow;
        NextRetryAtUtc = null;
    }

    public void RecordFailure(int? httpStatus, string error, int maxRetries = 5)
    {
        AttemptCount++;
        LastHttpStatus = httpStatus;
        LastError = error;

        if (AttemptCount >= maxRetries)
        {
            Status = WebhookDeliveryStatus.DeadLettered;
            NextRetryAtUtc = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
            // Backoff exponencial: 2^(tentativa - 1) * 2 segundos (ex: 2s, 4s, 8s, 16s, 32s)
            var delaySeconds = Math.Pow(2, AttemptCount - 1) * 2;
            NextRetryAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
        }
    }
}

