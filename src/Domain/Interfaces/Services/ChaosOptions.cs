using Domain.Enums;

namespace Domain.Interfaces.Services;

public record ChaosOptions(
    ChaosAction Action = ChaosAction.None,
    int DelayMilliseconds = 0,
    string? FailureReason = null,
    string? BankErrorCode = null
);

