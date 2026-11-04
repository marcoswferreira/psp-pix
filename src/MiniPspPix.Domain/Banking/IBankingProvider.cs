namespace MiniPspPix.Domain.Banking;

public enum ChaosAction
{
    None = 0,
    Delay = 1,
    Timeout = 2,
    Fail = 3,
    Duplicate = 4
}

public record ChaosOptions(
    ChaosAction Action = ChaosAction.None,
    int DelayMilliseconds = 0,
    string? FailureReason = null,
    string? BankErrorCode = null
);

public record BankChargeRequest(
    string TxId,
    decimal Amount,
    string PixKey,
    string Description,
    ChaosOptions? Chaos = null
);

public record BankChargeResult(
    bool Success,
    string? BankTransactionId,
    string? ErrorCode,
    string? ErrorMessage
);

public record BankPayoutRequest(
    Guid PayoutId,
    decimal Amount,
    string DestinationPixKey,
    string Description,
    ChaosOptions? Chaos = null
);

public record BankPayoutResult(
    bool Success,
    string? EndToEndId,
    string? BankTransactionId,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime SettledAtUtc
);

public record SimulatedInboundPaymentRequest(
    string TxId,
    decimal Amount,
    ChaosOptions? Chaos = null
);

public record SimulatedInboundPaymentResult(
    bool Success,
    string EndToEndId,
    string? BankTransactionId,
    bool WasDuplicated,
    string? ErrorMessage
);

public interface IBankingProvider
{
    Task<BankChargeResult> RegisterChargeAsync(BankChargeRequest request, CancellationToken ct = default);
    Task<BankPayoutResult> SendPayoutAsync(BankPayoutRequest request, CancellationToken ct = default);
    Task<SimulatedInboundPaymentResult> SimulateInboundPaymentAsync(SimulatedInboundPaymentRequest request, CancellationToken ct = default);
}

