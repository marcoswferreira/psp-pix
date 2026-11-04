using Domain.Enums;

namespace Application.UseCases.Ledger.Dto;

public record LedgerStatementItemOutput(
    Guid Id,
    Guid TransactionId,
    EntryType Type,
    decimal Amount,
    string Description,
    DateTime CreatedAtUtc
);

