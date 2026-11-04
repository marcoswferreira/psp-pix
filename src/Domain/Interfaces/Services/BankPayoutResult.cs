namespace Domain.Interfaces.Services;

public record BankPayoutResult(
    bool Success,
    string? EndToEndId,
    string? BankTransactionId,
    string? ErrorCode,
    string? ErrorMessage,
    DateTime SettledAtUtc
);

