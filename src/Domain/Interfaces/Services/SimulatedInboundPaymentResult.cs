namespace Domain.Interfaces.Services;

public record SimulatedInboundPaymentResult(
    bool Success,
    string EndToEndId,
    string? BankTransactionId,
    bool WasDuplicated,
    string? ErrorMessage
);

