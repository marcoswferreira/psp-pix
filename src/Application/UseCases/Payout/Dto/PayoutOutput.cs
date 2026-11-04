namespace Application.UseCases.Payout.Dto;

public record PayoutOutput(
    bool Success,
    Guid PayoutId,
    string? EndToEndId,
    decimal Amount,
    decimal RemainingBalance,
    string Message
);

