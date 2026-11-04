using FluentAssertions;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Ledger;

namespace MiniPspPix.UnitTests;

public class LedgerDomainTests
{
    [Fact]
    public void AssertBalanceInvariant_WithBalancedTransaction_ShouldSucceed()
    {
        var txId = Guid.NewGuid();
        var acc1 = Guid.NewGuid();
        var acc2 = Guid.NewGuid();

        var tx = new LedgerTransaction(txId, "PixTransfer", "REF-001");
        tx.AddEntry(new LedgerEntry(txId, acc1, EntryType.Debit, 250.00m, "Débito"));
        tx.AddEntry(new LedgerEntry(txId, acc2, EntryType.Credit, 250.00m, "Crédito"));

        var act = () => tx.AssertBalanceInvariant();

        act.Should().NotThrow();
    }

    [Fact]
    public void AssertBalanceInvariant_WithUnbalancedTransaction_ShouldThrowPixDomainException()
    {
        var txId = Guid.NewGuid();
        var acc1 = Guid.NewGuid();
        var acc2 = Guid.NewGuid();

        var tx = new LedgerTransaction(txId, "PixTransfer", "REF-002");
        tx.AddEntry(new LedgerEntry(txId, acc1, EntryType.Debit, 250.00m, "Débito"));
        tx.AddEntry(new LedgerEntry(txId, acc2, EntryType.Credit, 200.00m, "Crédito divergente"));

        var act = () => tx.AssertBalanceInvariant();

        act.Should().Throw<PixDomainException>().WithMessage("*Invariante do Ledger quebrada*");
    }

    [Fact]
    public void LedgerAccount_CreditAndDebit_ShouldUpdateBalanceAndIncrementVersion()
    {
        var account = new LedgerAccount(Guid.NewGuid(), "ACC-100", AccountType.Merchant, 500m);
        account.Version.Should().Be(1);

        account.Credit(100m);
        account.Balance.Should().Be(600m);
        account.Version.Should().Be(2);

        account.Debit(200m);
        account.Balance.Should().Be(400m);
        account.Version.Should().Be(3);
    }

    [Fact]
    public void LedgerAccount_DebitExceedingBalance_ShouldThrowInsufficientFundsException()
    {
        var account = new LedgerAccount(Guid.NewGuid(), "ACC-101", AccountType.Merchant, 100m);

        var act = () => account.Debit(150m);

        act.Should().Throw<InsufficientFundsException>().WithMessage("*Saldo insuficiente*");
    }

    [Fact]
    public void LedgerAccount_TransitAccountAllowOverdraft_ShouldAllowNegativeDebit()
    {
        var transitAccount = new LedgerAccount(Guid.NewGuid(), "TRANSIT-001", AccountType.PixSettlementTransit, 0m);

        transitAccount.Debit(500m, allowOverdraft: true);

        transitAccount.Balance.Should().Be(-500m);
    }
}

