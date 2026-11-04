using Domain.Enums;

namespace Domain.Interfaces.Services;

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

public interface IBrCodeService
{
    string GenerateBrCode(string pixKey, decimal amount, string merchantName, string merchantCity, string txId, string? description = null, bool isDynamic = false);
    string GenerateSvg(string payload, int size = 280);
    string ComputeCrc16Hex(string payload);
}

public interface IHmacSignerService
{
    string CreateSignature(string payloadJson, string secretKey, long? timestamp = null);
    bool VerifySignature(string payloadJson, string headerValue, string secretKey, TimeSpan? tolerance = null);
}

public interface IWebhookSenderClient
{
    Task<(int StatusCode, string? Error)> SendAsync(string url, string payloadJson, string signature, CancellationToken ct = default);
}
