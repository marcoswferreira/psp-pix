namespace Domain.Interfaces.Services;

public record SimulatedInboundPaymentRequest(
    string TxId,
    decimal Amount,
    ChaosOptions? Chaos = null
);

