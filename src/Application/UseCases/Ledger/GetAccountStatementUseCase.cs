using Application.UseCases.Ledger.Dto;
using Domain.Interfaces.Repositories;

namespace Application.UseCases.Ledger;

public class GetAccountStatementUseCase(ILedgerRepository ledgerRepository)
{
    private readonly ILedgerRepository _ledgerRepository = ledgerRepository;

    public async Task<IReadOnlyList<LedgerStatementItemOutput>> ExecuteAsync(Guid accountId, CancellationToken ct = default)
    {
        var entries = await _ledgerRepository.GetStatementAsync(accountId, ct);
        return entries
            .Select(e => new LedgerStatementItemOutput(e.Id, e.TransactionId, e.Type, e.Amount, e.Description, e.CreatedAtUtc))
            .ToList();
    }
}

