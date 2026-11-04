using Microsoft.EntityFrameworkCore;
using MiniPspPix.Application.Ledger;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Ledger;
using MiniPspPix.Infrastructure.Persistence;

namespace MiniPspPix.Infrastructure.Ledger;

public class EfCoreLedgerService : ILedgerService
{
    private readonly PixDbContext _dbContext;

    public EfCoreLedgerService(PixDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LedgerTransaction> RecordDoubleEntryTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default)
    {
        if (amount <= 0)
            throw new PixDomainException("Valor da transferência deve ser maior que zero.");

        var sourceAccount = await _dbContext.LedgerAccounts.FindAsync(new object[] { sourceAccountId }, ct)
            ?? throw new PixDomainException($"Conta de origem {sourceAccountId} não encontrada.");

        var destAccount = await _dbContext.LedgerAccounts.FindAsync(new object[] { destinationAccountId }, ct)
            ?? throw new PixDomainException($"Conta de destino {destinationAccountId} não encontrada.");

        // Atualização dos saldos com validação e incremento de versão
        sourceAccount.Debit(amount, allowOverdraft: sourceAccount.Type == AccountType.PixSettlementTransit);
        destAccount.Credit(amount);

        // Registro da Transação e Entradas de Dupla Entrada
        var transactionId = Guid.NewGuid();
        var transaction = new LedgerTransaction(transactionId, referenceType, referenceId);

        var debitEntry = new LedgerEntry(transactionId, sourceAccountId, EntryType.Debit, amount, $"Débito: {description}");
        var creditEntry = new LedgerEntry(transactionId, destinationAccountId, EntryType.Credit, amount, $"Crédito: {description}");

        transaction.AddEntry(debitEntry);
        transaction.AddEntry(creditEntry);

        // Validação da invariante fundamental do Ledger: Débitos == Créditos
        transaction.AssertBalanceInvariant();

        _dbContext.LedgerTransactions.Add(transaction);
        _dbContext.LedgerEntries.AddRange(debitEntry, creditEntry);

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Conflito de concorrência detectado: o saldo da conta foi modificado por outra transação.", ex);
        }

        return transaction;
    }

    public async Task<LedgerAccount?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _dbContext.LedgerAccounts.FindAsync(new object[] { accountId }, ct);
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _dbContext.LedgerAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new PixDomainException($"Conta {accountId} não encontrada.");

        return account.Balance;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
    }

    public async Task UnsafeTransferAsync(
        Guid sourceAccountId,
        Guid destinationAccountId,
        decimal amount,
        string referenceType,
        string referenceId,
        string description,
        CancellationToken ct = default)
    {
        // Simulação de sistema ingênuo sem controle de concorrência:
        // Lê os dados, sofre race condition e sobrescreve o saldo
        var source = await _dbContext.LedgerAccounts.FindAsync(new object[] { sourceAccountId }, ct);
        var dest = await _dbContext.LedgerAccounts.FindAsync(new object[] { destinationAccountId }, ct);

        if (source == null || dest == null)
            throw new PixDomainException("Conta não encontrada.");

        // Janela de colisão para evidenciar a corrida entre threads
        await Task.Delay(15, ct);

        source.UnsafeDirectDebit(amount);
        dest.Credit(amount);

        // Bypassa o token de concorrência sincronizando com o valor atual do banco para simular ausência de lock/versão
        var freshSourceVersion = await _dbContext.LedgerAccounts.AsNoTracking()
            .Where(a => a.Id == sourceAccountId)
            .Select(a => a.Version)
            .FirstOrDefaultAsync(ct);

        _dbContext.Entry(source).Property(a => a.Version).OriginalValue = freshSourceVersion;

        await _dbContext.SaveChangesAsync(ct);
    }
}

