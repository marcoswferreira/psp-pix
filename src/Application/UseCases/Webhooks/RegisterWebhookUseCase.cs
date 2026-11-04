using Application.UseCases.Webhooks.Dto;
using Domain.Entities;
using Domain.Interfaces.Repositories;

namespace Application.UseCases.Webhooks;

public class RegisterWebhookUseCase(IWebhookRepository webhookRepository)
{
    private readonly IWebhookRepository _webhookRepository = webhookRepository;

    public async Task<WebhookSubscriptionOutput> ExecuteAsync(RegisterWebhookInput input, CancellationToken ct = default)
    {
        var subscription = new WebhookSubscription(input.MerchantId, input.TargetUrl, input.SecretKey);
        await _webhookRepository.AddSubscriptionAsync(subscription, ct);

        return new WebhookSubscriptionOutput(
            subscription.Id,
            subscription.MerchantId,
            subscription.TargetUrl,
            subscription.SecretKey,
            subscription.IsActive,
            subscription.CreatedAtUtc
        );
    }
}

