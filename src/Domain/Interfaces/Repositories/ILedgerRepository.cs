using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface ILedgerRepository
{
    Task<LedgerAccount?> GetAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(Guid accountId, CancellationToken ct = default);
    Task<LedgerTransaction> RecordDoubleEntryTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default);

    Task UnsafeTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default);
}

