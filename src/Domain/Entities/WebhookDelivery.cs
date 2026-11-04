using Domain.Common.Entities;
using Domain.Enums;

namespace Domain.Entities;

public class WebhookDelivery : BaseEntity, IAggregateRoot
{
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
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordFailure(int? httpStatus, string error, int maxRetries = 5)
    {
        AttemptCount++;
        LastHttpStatus = httpStatus;
        LastError = error;
        UpdatedAtUtc = DateTime.UtcNow;

        if (AttemptCount >= maxRetries)
        {
            Status = WebhookDeliveryStatus.DeadLettered;
            NextRetryAtUtc = null;
        }
        else
        {
            Status = WebhookDeliveryStatus.Failed;
            var delaySeconds = Math.Pow(2, AttemptCount - 1) * 2;
            NextRetryAtUtc = DateTime.UtcNow.AddSeconds(delaySeconds);
        }
    }
}

