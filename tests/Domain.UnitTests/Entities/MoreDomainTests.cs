using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;

namespace Domain.UnitTests.Entities;

public class MoreDomainTests
{
    [Fact]
    public void DomainExceptions_Constructors_ShouldPreserveMessages()
    {
        var ex1 = new ConcurrencyException("Concorrência");
        ex1.Message.Should().Be("Concorrência");

        var inner = new Exception("Inner");
        var ex2 = new ConcurrencyException("Concorrência com inner", inner);
        ex2.InnerException.Should().Be(inner);

        var ex3 = new IdempotencyMismatchException("Mismatch");
        ex3.Message.Should().Be("Mismatch");

        var ex4 = new IdempotencyConflictException("Conflict");
        ex4.Message.Should().Be("Conflict");

        var ex5 = new DomainException("Domain com inner", inner);
        ex5.InnerException.Should().Be(inner);

        var ex6 = new InsufficientFundsException("Saldo com inner", inner);
        ex6.InnerException.Should().Be(inner);
    }

    [Fact]
    public void WebhookSubscription_Lifecycle_ShouldWork()
    {
        var sub = new WebhookSubscription("MERCH-01", "https://loja.com/hook", "secretKey");
        sub.MerchantId.Should().Be("MERCH-01");
        sub.TargetUrl.Should().Be("https://loja.com/hook");
        sub.SecretKey.Should().Be("secretKey");
        sub.IsActive.Should().BeTrue();

        sub.Deactivate();
        sub.IsActive.Should().BeFalse();
        sub.UpdatedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void WebhookSubscription_WithoutSecret_ShouldAutoGenerateSecret()
    {
        var sub = new WebhookSubscription("MERCH-02", "https://loja.com/hook2");
        sub.SecretKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void IdempotencyRecord_LockAndCompletion_ShouldUpdateState()
    {
        var record = new IdempotencyRecord(
            "key-123",
            "hash-abc",
            "/api/v1/charges",
            TimeSpan.FromMinutes(2)
        );

        record.Key.Should().Be("key-123");
        record.RequestHash.Should().Be("hash-abc");
        record.Route.Should().Be("/api/v1/charges");
        record.Status.Should().Be(IdempotencyStatus.InProgress);
        record.LockedUntilUtc.Should().NotBeNull();

        record.Complete(201, "{\"txId\":\"123\"}");

        record.Status.Should().Be(IdempotencyStatus.Completed);
        record.StatusCode.Should().Be(201);
        record.ResponseBody.Should().Be("{\"txId\":\"123\"}");
    }

    [Fact]
    public void LedgerEntry_Properties_ShouldBeCorrect()
    {
        var txId = Guid.NewGuid();
        var accId = Guid.NewGuid();
        var entry = new LedgerEntry(txId, accId, EntryType.Credit, 100m, "Crédito Teste");

        entry.TransactionId.Should().Be(txId);
        entry.AccountId.Should().Be(accId);
        entry.Type.Should().Be(EntryType.Credit);
        entry.Amount.Should().Be(100m);
        entry.Description.Should().Be("Crédito Teste");
    }

    [Fact]
    public void LedgerTransaction_Properties_ShouldBeCorrect()
    {
        var id = Guid.NewGuid();
        var tx = new LedgerTransaction(id, "PIX_PAYOUT", "REF_999");

        tx.Id.Should().Be(id);
        tx.ReferenceType.Should().Be("PIX_PAYOUT");
        tx.ReferenceId.Should().Be("REF_999");
    }
}
