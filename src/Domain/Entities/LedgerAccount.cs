using Domain.Common.Entities;
using Domain.Common.Exceptions;
using Domain.Enums;

namespace Domain.Entities;

public class LedgerAccount : BaseEntity, IAggregateRoot
{
    public string AccountNumber { get; private set; }
    public AccountType Type { get; private set; }
    public decimal Balance { get; private set; }
    public string Currency { get; private set; }
    public uint Version { get; private set; }

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
            throw new DomainException("Valor de crédito no ledger deve ser positivo.");

        Balance += amount;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Debit(decimal amount, bool? allowOverdraft = null)
    {
        if (amount <= 0)
            throw new DomainException("Valor de débito no ledger deve ser positivo.");

        var canOverdraft = allowOverdraft ?? (Type == AccountType.PixSettlementTransit);

        if (!canOverdraft && Balance < amount)
            throw new InsufficientFundsException($"Saldo insuficiente na conta {AccountNumber}. Saldo: {Balance:C}, Débito solicitado: {amount:C}.");

        Balance -= amount;
        Version++;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void UnsafeDirectDebit(decimal amount)
    {
        Balance -= amount;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}

