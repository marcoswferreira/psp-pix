using Domain.Common.Entities;
using Domain.Common.Exceptions;
using Domain.Enums;

namespace Domain.Entities;

public class LedgerTransaction : BaseEntity, IAggregateRoot
{
    private readonly List<LedgerEntry> _entries = new();

    public string ReferenceType { get; private set; }
    public string ReferenceId { get; private set; }
    public IReadOnlyCollection<LedgerEntry> Entries => _entries.AsReadOnly();

    protected LedgerTransaction()
    {
        ReferenceType = null!;
        ReferenceId = null!;
    }

    public LedgerTransaction(Guid id, string referenceType, string referenceId)
    {
        Id = id;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void AddEntry(LedgerEntry entry)
    {
        _entries.Add(entry);
    }

    public void AssertBalanceInvariant()
    {
        var totalDebits = _entries.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount);
        var totalCredits = _entries.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount);

        if (totalDebits != totalCredits)
        {
            throw new DomainException($"Invariante do Ledger quebrada! Débitos ({totalDebits:F2}) != Créditos ({totalCredits:F2}).");
        }
    }
}

