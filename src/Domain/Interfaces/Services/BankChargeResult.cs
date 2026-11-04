namespace Domain.Interfaces.Services;

public record BankChargeResult(
    bool Success,
    string? BankTransactionId,
    string? ErrorCode,
    string? ErrorMessage
);

