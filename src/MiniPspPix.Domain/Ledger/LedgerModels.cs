using MiniPspPix.Domain.Exceptions;

namespace MiniPspPix.Domain.Ledger;

public enum AccountType
{
    Merchant = 1,
    PixSettlementTransit = 2,
    PlatformFees = 3
}

public enum EntryType
{
    Debit = 1,
    Credit = 2
}

public class LedgerAccount
{
    public Guid Id { get; private set; }
    public string AccountNumber { get; private set; }
    public AccountType Type { get; private set; }
    public decimal Balance { get; private set; }
    public string Currency { get; private set; }
    public uint Version { get; private set; } // Concorrência Otimista (RowVersion)
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    protected LedgerAccount()
    {
        AccountNumber = null!;
        Currency = "BRL";
    }

    public LedgerAccount(Guid id, string accountNumber, AccountType type, decimal initialBalance = 0, string currency = "BRL")
    {
        Id = id;
        AccountNumber = accountNumber;
        Type = type;
        Balance = initialBalance;
        Currency = currency;
        Version = 1;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Credit(decimal amount)
    {
        if (amount <= 0)
            throw new PixDomainException("Valor de crédito no ledger deve ser positivo.");

        Balance += amount;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Debit(decimal amount, bool allowOverdraft = false)
    {
        if (amount <= 0)
            throw new PixDomainException("Valor de débito no ledger deve ser positivo.");

        if (!allowOverdraft && Balance < amount)
            throw new InsufficientFundsException($"Saldo insuficiente na conta {AccountNumber}. Saldo: {Balance:C}, Débito solicitado: {amount:C}.");

        Balance -= amount;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Método intencionalmente sem verificação de concorrência ou saldo para demonstrar race conditions em testes.
    /// </summary>
    public void UnsafeDirectDebit(decimal amount)
    {
        Balance -= amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

public class LedgerEntry
{
    public Guid Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public Guid AccountId { get; private set; }
    public EntryType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    protected LedgerEntry()
    {
        Description = null!;
    }

    public LedgerEntry(Guid transactionId, Guid accountId, EntryType type, decimal amount, string description)
    {
        if (amount <= 0)
            throw new PixDomainException("Valor do lançamento no ledger deve ser positivo.");

        Id = Guid.NewGuid();
        TransactionId = transactionId;
        AccountId = accountId;
        Type = type;
        Amount = amount;
        Description = description;
        CreatedAtUtc = DateTime.UtcNow;
    }
}

public class LedgerTransaction
{
    private readonly List<LedgerEntry> _entries = new();

    public Guid Id { get; private set; }
    public string ReferenceType { get; private set; }
    public string ReferenceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
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

    /// <summary>
    /// Validação do princípio de dupla entrada contábil: Soma dos débitos DEVE ser igual à soma dos créditos.
    /// </summary>
    public void AssertBalanceInvariant()
    {
        var totalDebits = _entries.Where(e => e.Type == EntryType.Debit).Sum(e => e.Amount);
        var totalCredits = _entries.Where(e => e.Type == EntryType.Credit).Sum(e => e.Amount);

        if (totalDebits != totalCredits)
        {
            throw new PixDomainException($"Invariante do Ledger quebrada! Débitos ({totalDebits:F2}) != Créditos ({totalCredits:F2}). A transação contábil não balanceia.");
        }
    }
}

