using MiniPspPix.Domain.Ledger;

namespace MiniPspPix.Application.Ledger;

public interface ILedgerService
{
    Task<LedgerTransaction> RecordDoubleEntryTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default);

    Task<LedgerAccount?> GetAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>
    /// Método sem lock ou controle de versão de linha, usado para demonstrar a falha de corrida (race condition / double spending) em testes.
    /// </summary>
    Task UnsafeTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default);
}

