namespace Application.UseCases.Webhooks.Dto;

public record RegisterWebhookInput(
    string MerchantId,
    string TargetUrl,
    string? SecretKey = null
);

