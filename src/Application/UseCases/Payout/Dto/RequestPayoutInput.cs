using Domain.Interfaces.Services;

namespace Application.UseCases.Payout.Dto;

public record RequestPayoutInput(
    Guid MerchantAccountId,
    decimal Amount,
    string DestinationPixKey,
    string Description,
    ChaosOptions? Chaos = null
);

