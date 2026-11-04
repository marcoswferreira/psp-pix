using Domain.Enums;

namespace Application.UseCases.Ledger.Dto;

public record AccountBalanceOutput(
    Guid Id,
    string AccountNumber,
    AccountType Type,
    decimal Balance,
    string Currency,
    uint Version
);

