using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;

namespace Application.UseCases.Webhooks.Dto;

public record RegisterWebhookInput(
    string MerchantId,
    string TargetUrl,
    string? SecretKey = null
);

public record WebhookSubscriptionOutput(
    Guid Id,
    string MerchantId,
    string TargetUrl,
    string SecretKey,
    bool IsActive,
    DateTime CreatedAtUtc
);
