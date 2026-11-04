using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using FluentAssertions;

namespace Domain.UnitTests.Entities;

public class EntityTests
{
    [Fact]
    public void PixCharge_Creation_ShouldSetInitialStateCorrectly()
    {
        var txId = TxId.NewTxId();
        var pixKey = new PixKey("12345678901");
        var charge = new PixCharge(
            txId,
            100.50m,
            pixKey,
            "Teste de Cobrança",
            "Loja Tech",
            "Sao Paulo",
            "00020126...BRCode",
            "<svg>QR</svg>",
            TimeSpan.FromHours(1)
        );

        charge.TxId.Should().Be(txId.Value);
        charge.Amount.Should().Be(100.50m);
        charge.PixKeyValue.Should().Be(pixKey.Value);
        charge.Status.Should().Be(ChargeStatus.Active);
    }

    [Fact]
    public void PixCharge_MarkAsPaid_ShouldUpdateStatusAndEndToEndId()
    {
        var charge = new PixCharge(
            TxId.NewTxId(),
            50.00m,
            new PixKey("teste@pix.com"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg",
            TimeSpan.FromHours(1)
        );

        var e2e = new EndToEndId("E1234567820261104120000000000001");
        var paidAt = DateTime.UtcNow;
        charge.MarkAsPaid(e2e, paidAt);

        charge.Status.Should().Be(ChargeStatus.Completed);
        charge.EndToEndId.Should().Be(e2e.Value);
        charge.PaidAtUtc.Should().Be(paidAt);
    }

    [Fact]
    public void PixCharge_MarkAsPaid_WhenAlreadyPaid_ShouldBeIdempotent()
    {
        var charge = new PixCharge(
            TxId.NewTxId(),
            50.00m,
            new PixKey("teste@pix.com"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg",
            TimeSpan.FromHours(1)
        );

        var e2e1 = new EndToEndId("E1234567820261104120000000000001");
        charge.MarkAsPaid(e2e1, DateTime.UtcNow);

        var e2e2 = new EndToEndId("E1234567820261104120000000000002");
        charge.MarkAsPaid(e2e2, DateTime.UtcNow);

        charge.Status.Should().Be(ChargeStatus.Completed);
        charge.EndToEndId.Should().Be(e2e1.Value);
    }

    [Fact]
    public void PixCharge_Expire_WhenActive_ShouldTransitionToExpired()
    {
        var charge = new PixCharge(
            TxId.NewTxId(),
            50.00m,
            new PixKey("teste@pix.com"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg",
            TimeSpan.FromHours(1)
        );

        charge.Expire();

        charge.Status.Should().Be(ChargeStatus.Expired);
    }

    [Fact]
    public void PixCharge_MarkAsPaid_WhenExpired_ShouldThrowDomainException()
    {
        var charge = new PixCharge(
            TxId.NewTxId(),
            50.00m,
            new PixKey("teste@pix.com"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg",
            TimeSpan.FromHours(1)
        );

        charge.Expire();

        var act = () => charge.MarkAsPaid(new EndToEndId("E1234567820261104120000000000001"), DateTime.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("*expirada*");
    }

    [Fact]
    public void LedgerAccount_CreditAndDebit_ShouldUpdateBalanceAndIncrementVersion()
    {
        var account = new LedgerAccount(Guid.NewGuid(), "ACC-001", AccountType.Merchant, 100.00m);
        account.Version.Should().Be(1);

        account.Credit(50.00m);
        account.Balance.Should().Be(150.00m);
        account.Version.Should().Be(2);

        account.Debit(30.00m);
        account.Balance.Should().Be(120.00m);
        account.Version.Should().Be(3);
    }

    [Fact]
    public void LedgerAccount_Debit_WithInsufficientFunds_ShouldThrowInsufficientFundsException()
    {
        var account = new LedgerAccount(Guid.NewGuid(), "ACC-001", AccountType.Merchant, 50.00m);

        var act = () => account.Debit(100.00m);

        act.Should().Throw<InsufficientFundsException>()
            .WithMessage("*Saldo insuficiente*");
    }

    [Fact]
    public void LedgerAccount_Debit_WithTransitAccount_ShouldAllowNegativeBalance()
    {
        var account = new LedgerAccount(Guid.NewGuid(), "TRANSIT-001", AccountType.PixSettlementTransit, 0m);

        account.Debit(200.00m);

        account.Balance.Should().Be(-200.00m);
    }

    [Fact]
    public void LedgerTransaction_DoubleEntryInvariant_ShouldSucceedWhenBalanced()
    {
        var sourceAccId = Guid.NewGuid();
        var destAccId = Guid.NewGuid();
        var tx = new LedgerTransaction(Guid.NewGuid(), "PIX_INBOUND", "TX123");

        tx.AddEntry(new LedgerEntry(tx.Id, sourceAccId, EntryType.Debit, 100m, "Debit Transit"));
        tx.AddEntry(new LedgerEntry(tx.Id, destAccId, EntryType.Credit, 100m, "Credit Merchant"));

        tx.AssertBalanceInvariant(); // Should not throw
        tx.Entries.Should().HaveCount(2);
    }

    [Fact]
    public void LedgerTransaction_DoubleEntryInvariant_ShouldThrowWhenUnbalanced()
    {
        var sourceAccId = Guid.NewGuid();
        var destAccId = Guid.NewGuid();
        var tx = new LedgerTransaction(Guid.NewGuid(), "PIX_INBOUND", "TX123");

        tx.AddEntry(new LedgerEntry(tx.Id, sourceAccId, EntryType.Debit, 100m, "Debit"));
        tx.AddEntry(new LedgerEntry(tx.Id, destAccId, EntryType.Credit, 90m, "Credit"));

        var act = () => tx.AssertBalanceInvariant();
        act.Should().Throw<DomainException>().WithMessage("*Invariante do Ledger quebrada*");
    }

    [Fact]
    public void WebhookDelivery_RetryMechanism_ShouldDeadLetterAfterMaxRetries()
    {
        var delivery = new WebhookDelivery(
            Guid.NewGuid(),
            "https://merchant.com/webhook",
            "pix.payment.received",
            "{\"amount\":100}",
            "t=1,v1=signature"
        );

        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(0);

        // Attempts 1 to 4 should be Failed
        for (int i = 1; i <= 4; i++)
        {
            delivery.RecordFailure(500, "Internal Server Error", maxRetries: 5);
            delivery.AttemptCount.Should().Be(i);
            delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
            delivery.NextRetryAtUtc.Should().NotBeNull();
        }

        // 5th attempt reaches maxRetries -> DeadLettered
        delivery.RecordFailure(500, "Internal Server Error", maxRetries: 5);
        delivery.AttemptCount.Should().Be(5);
        delivery.Status.Should().Be(WebhookDeliveryStatus.DeadLettered);
        delivery.NextRetryAtUtc.Should().BeNull();
    }

    [Fact]
    public void WebhookDelivery_RecordSuccess_ShouldUpdateStatusAndDeliveredTime()
    {
        var delivery = new WebhookDelivery(
            Guid.NewGuid(),
            "https://merchant.com/webhook",
            "pix.payment.received",
            "{}",
            "signature"
        );

        delivery.RecordSuccess(200);

        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        delivery.LastHttpStatus.Should().Be(200);
        delivery.DeliveredAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void OutboxMessage_ProcessingAndFailure_ShouldUpdateState()
    {
        var msg = new OutboxMessage("pix.created", "{\"txId\":\"123\"}");

        msg.ProcessedAtUtc.Should().BeNull();
        msg.RetryCount.Should().Be(0);

        msg.RecordFailure("Connection refused");
        msg.RetryCount.Should().Be(1);
        msg.LastError.Should().Be("Connection refused");

        msg.MarkAsProcessed();
        msg.ProcessedAtUtc.Should().NotBeNull();
    }
}
