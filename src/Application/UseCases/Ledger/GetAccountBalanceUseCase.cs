using Application.UseCases.Ledger.Dto;
using Domain.Interfaces.Repositories;

namespace Application.UseCases.Ledger;

public class GetAccountBalanceUseCase(ILedgerRepository ledgerRepository)
{
    private readonly ILedgerRepository _ledgerRepository = ledgerRepository;

    public async Task<AccountBalanceOutput?> ExecuteAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _ledgerRepository.GetAccountAsync(accountId, ct);
        return account != null
            ? new AccountBalanceOutput(account.Id, account.AccountNumber, account.Type, account.Balance, account.Currency, account.Version)
            : null;
    }
}

