namespace Domain.Enums;

public enum WebhookDeliveryStatus
{
    Pending = 1,
    Delivered = 2,
    Failed = 3,
    DeadLettered = 4
}

