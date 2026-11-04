namespace Domain.Interfaces.Services;

public record BankChargeRequest(
    string TxId,
    decimal Amount,
    string PixKey,
    string Description,
    ChaosOptions? Chaos = null
);

