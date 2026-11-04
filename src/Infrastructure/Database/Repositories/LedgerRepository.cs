using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class LedgerRepository(ApplicationDbContext db) : ILedgerRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<LedgerAccount?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _db.LedgerAccounts.FindAsync(new object[] { accountId }, ct);
    }

    public async Task<decimal> GetBalanceAsync(Guid accountId, CancellationToken ct = default)
    {
        var account = await _db.LedgerAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new DomainException($"Conta {accountId} não encontrada.");

        return account.Balance;
    }

    public async Task<IReadOnlyList<LedgerEntry>> GetStatementAsync(Guid accountId, CancellationToken ct = default)
    {
        return await _db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(ct);
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
            throw new DomainException("Valor da transferência deve ser maior que zero.");

        var sourceAccount = await _db.LedgerAccounts.FindAsync(new object[] { sourceAccountId }, ct)
            ?? throw new DomainException($"Conta de origem {sourceAccountId} não encontrada.");

        var destAccount = await _db.LedgerAccounts.FindAsync(new object[] { destinationAccountId }, ct)
            ?? throw new DomainException($"Conta de destino {destinationAccountId} não encontrada.");

        sourceAccount.Debit(amount, allowOverdraft: sourceAccount.Type == AccountType.PixSettlementTransit);
        destAccount.Credit(amount);

        var transactionId = Guid.NewGuid();
        var transaction = new LedgerTransaction(transactionId, referenceType, referenceId);

        var debitEntry = new LedgerEntry(transactionId, sourceAccountId, EntryType.Debit, amount, $"Débito: {description}");
        var creditEntry = new LedgerEntry(transactionId, destinationAccountId, EntryType.Credit, amount, $"Crédito: {description}");

        transaction.AddEntry(debitEntry);
        transaction.AddEntry(creditEntry);
        transaction.AssertBalanceInvariant();

        _db.LedgerTransactions.Add(transaction);
        _db.LedgerEntries.AddRange(debitEntry, creditEntry);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("Conflito de concorrência detectado: o saldo da conta foi modificado por outra transação.", ex);
        }

        return transaction;
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
        var source = await _db.LedgerAccounts.FindAsync(new object[] { sourceAccountId }, ct);
        var dest = await _db.LedgerAccounts.FindAsync(new object[] { destinationAccountId }, ct);

        if (source == null || dest == null)
            throw new DomainException("Conta não encontrada.");

        await Task.Delay(15, ct);

        source.UnsafeDirectDebit(amount);
        dest.Credit(amount);

        var freshSourceVersion = await _db.LedgerAccounts.AsNoTracking()
            .Where(a => a.Id == sourceAccountId)
            .Select(a => a.Version)
            .FirstOrDefaultAsync(ct);

        _db.Entry(source).Property(a => a.Version).OriginalValue = freshSourceVersion;

        await _db.SaveChangesAsync(ct);
    }
}

