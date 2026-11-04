namespace Domain.Interfaces.Services;

public record BankPayoutRequest(
    Guid PayoutId,
    decimal Amount,
    string DestinationPixKey,
    string Description,
    ChaosOptions? Chaos = null
);

