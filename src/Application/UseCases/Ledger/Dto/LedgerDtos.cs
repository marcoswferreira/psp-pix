using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;

namespace Application.UseCases.Ledger.Dto;

public record AccountBalanceOutput(
    Guid Id,
    string AccountNumber,
    AccountType Type,
    decimal Balance,
    string Currency,
    uint Version
);

public record LedgerStatementItemOutput(
    Guid Id,
    Guid TransactionId,
    EntryType Type,
    decimal Amount,
    string Description,
    DateTime CreatedAtUtc
);

