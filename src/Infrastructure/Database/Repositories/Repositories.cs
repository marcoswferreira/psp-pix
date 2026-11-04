using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class ChargeRepository(ApplicationDbContext db) : IChargeRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task AddAsync(PixCharge charge, CancellationToken ct = default)
    {
        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PixCharge?> GetByTxIdAsync(string txId, CancellationToken ct = default)
    {
        return await _db.Charges.FirstOrDefaultAsync(c => c.TxId == txId, ct);
    }

    public async Task<PixCharge?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Charges.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(PixCharge charge, CancellationToken ct = default)
    {
        _db.Charges.Update(charge);
        await _db.SaveChangesAsync(ct);
    }
}

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

public class OutboxRepository(ApplicationDbContext db) : IOutboxRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        return await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (msg != null)
        {
            msg.MarkAsProcessed();
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RecordFailureAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (msg != null)
        {
            msg.RecordFailure(error);
            await _db.SaveChangesAsync(ct);
        }
    }
}

public class WebhookRepository(ApplicationDbContext db) : IWebhookRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task AddSubscriptionAsync(WebhookSubscription subscription, CancellationToken ct = default)
    {
        _db.WebhookSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<WebhookSubscription?> GetSubscriptionByMerchantIdAsync(string merchantId, CancellationToken ct = default)
    {
        return await _db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.MerchantId == merchantId && s.IsActive, ct);
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken ct = default)
    {
        return await _db.WebhookSubscriptions.Where(s => s.IsActive).ToListAsync(ct);
    }

    public async Task AddDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateDeliveryAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _db.WebhookDeliveries.Update(delivery);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WebhookDelivery>> GetPendingDeliveriesAsync(int batchSize = 10, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.WebhookDeliveries
            .Where(d => (d.Status == WebhookDeliveryStatus.Pending || d.Status == WebhookDeliveryStatus.Failed)
                        && (d.NextRetryAtUtc == null || d.NextRetryAtUtc <= now))
            .OrderBy(d => d.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }
}

public class IdempotencyStore(ApplicationDbContext db) : IIdempotencyStore
{
    private readonly ApplicationDbContext _db = db;

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        return await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);
    }

    public async Task<bool> TryAcquireLockAsync(string key, string requestHash, string route, TimeSpan lockDuration, CancellationToken ct = default)
    {
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);

        if (existing == null)
        {
            var record = new IdempotencyRecord(key, requestHash, route, lockDuration);
            _db.IdempotencyRecords.Add(record);
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        if (existing.Status == IdempotencyStatus.InProgress && existing.HasLockExpired())
        {
            _db.IdempotencyRecords.Remove(existing);
            await _db.SaveChangesAsync(ct);

            var newRecord = new IdempotencyRecord(key, requestHash, route, lockDuration);
            _db.IdempotencyRecords.Add(newRecord);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        return false;
    }

    public async Task SaveResultAsync(string key, int statusCode, string responseBody, CancellationToken ct = default)
    {
        var record = await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);
        if (record != null)
        {
            record.Complete(statusCode, responseBody);
            await _db.SaveChangesAsync(ct);
        }
    }
}
