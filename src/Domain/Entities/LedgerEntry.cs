using Domain.Common.Entities;
using Domain.Common.Exceptions;
using Domain.Enums;

namespace Domain.Entities;

public class LedgerEntry : BaseEntity
{
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public EntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; }

    protected LedgerEntry()
    {
        Description = null!;
    }

    public LedgerEntry(Guid transactionId, Guid accountId, EntryType type, decimal amount, string description)
    {
        if (amount <= 0)
            throw new DomainException("Valor do lançamento no ledger deve ser positivo.");

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        AccountId = accountId;
        Type = type;
        Amount = amount;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
    }
}

