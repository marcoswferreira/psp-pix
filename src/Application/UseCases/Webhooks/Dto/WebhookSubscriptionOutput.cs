namespace Application.UseCases.Webhooks.Dto;

public record WebhookSubscriptionOutput(
    Guid Id,
    string MerchantId,
    string TargetUrl,
    string SecretKey,
    bool IsActive,
    DateTime CreatedAtUtc
);

