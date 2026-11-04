using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MiniPspPix.Domain.Banking;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Idempotency;
using MiniPspPix.Domain.Ledger;
using MiniPspPix.Domain.Outbox;
using MiniPspPix.Domain.Webhooks;
using MiniPspPix.Infrastructure.Messaging;

namespace MiniPspPix.IntegrationTests;

public class DomainAndInfraCoverageTests
{
    [Fact]
    public void WebhookSubscriptionAndDelivery_AllMethods_ShouldBeCovered()
    {
        var sub = new WebhookSubscription("M99", "https://wh.com", "secret");
        sub.IsActive.Should().BeTrue();
        sub.Deactivate();
        sub.IsActive.Should().BeFalse();

        var delivery = new WebhookDelivery(sub.Id, sub.TargetUrl, "event", "{}", "sig");
        delivery.RecordSuccess(200);
        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);

        var deliveryFail = new WebhookDelivery(sub.Id, sub.TargetUrl, "event", "{}", "sig");
        deliveryFail.RecordFailure(500, "Error 1", maxRetries: 2);
        deliveryFail.Status.Should().Be(WebhookDeliveryStatus.Failed);
        deliveryFail.RecordFailure(500, "Error 2", maxRetries: 2);
        deliveryFail.Status.Should().Be(WebhookDeliveryStatus.DeadLettered);
    }

    [Fact]
    public void OutboxMessage_AllMethods_ShouldBeCovered()
    {
        var msg = new OutboxMessage("test.event", "{\"k\":1}");
        msg.RetryCount.Should().Be(0);
        msg.MarkAsProcessed();
        msg.ProcessedAtUtc.Should().NotBeNull();

        var msgFail = new OutboxMessage("test.event", "{}");
        msgFail.RecordFailure("Error");
        msgFail.RetryCount.Should().Be(1);
        msgFail.LastError.Should().Be("Error");
    }

    [Fact]
    public void LedgerTransactionAndEntries_AllMethods_ShouldBeCovered()
    {
        var tx = new LedgerTransaction(Guid.NewGuid(), "Pix", "Ref");
        var e1 = new LedgerEntry(tx.Id, Guid.NewGuid(), EntryType.Debit, 100m, "D");
        var e2 = new LedgerEntry(tx.Id, Guid.NewGuid(), EntryType.Credit, 100m, "C");
        tx.AddEntry(e1);
        tx.AddEntry(e2);
        tx.AssertBalanceInvariant();

        tx.Entries.Count.Should().Be(2);
    }

    [Fact]
    public void IdempotencyRecord_AllMethods_ShouldBeCovered()
    {
        var rec = new IdempotencyRecord("k", "h", "/r", TimeSpan.FromSeconds(1));
        rec.Status.Should().Be(IdempotencyStatus.InProgress);
        rec.Fail(500, "Error");
        rec.Status.Should().Be(IdempotencyStatus.Failed);
    }

    [Fact]
    public void DomainExceptions_Constructors_ShouldBeCovered()
    {
        var ex = new Exception("Inner");
        new PixDomainException("M", ex).InnerException.Should().Be(ex);
        new InsufficientFundsException("M", ex).InnerException.Should().Be(ex);
        new ConcurrencyException("M", ex).InnerException.Should().Be(ex);
        new IdempotencyMismatchException("M", ex).InnerException.Should().Be(ex);
        new IdempotencyConflictException("M", ex).InnerException.Should().Be(ex);
        new IdempotencyMismatchException("M").Message.Should().Be("M");
        new IdempotencyConflictException("M").Message.Should().Be("M");
    }

    [Fact]
    public void PixKeyAndTxId_Variants_ShouldBeCovered()
    {
        new PixKey("12345678909").Type.Should().Be(PixKeyType.Cpf);
        new PixKey("12345678000195").Type.Should().Be(PixKeyType.Cnpj);
        new PixKey("test@pix.com").Type.Should().Be(PixKeyType.Email);
        new PixKey("+5511999998888").Type.Should().Be(PixKeyType.Phone);
        new PixKey(Guid.NewGuid().ToString()).Type.Should().Be(PixKeyType.Random);

        var txId = new TxId("VALIDTXID123");
        txId.ToString().Should().Be("VALIDTXID123");

        var act = () => new TxId("INVALID TX ID WITH SPACES AND $$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$$");
        act.Should().Throw<PixDomainException>();
    }

    [Fact]
    public void BankingRecords_ShouldBeCovered()
    {
        var chaos = new ChaosOptions(ChaosAction.None, 0, null, null);
        var req = new BankChargeRequest("TX", 10m, "k", "d", chaos);
        var res = new BankChargeResult(true, "ID", null, null);

        req.TxId.Should().Be("TX");
        res.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RabbitMqAndInMemoryPublishers_ShouldBeCovered()
    {
        var pub = new RabbitMqEventPublisher("amqp://guest:guest@localhost:5679/", NullLogger<RabbitMqEventPublisher>.Instance);
        await pub.PublishAsync("test", "{}");
        pub.Dispose();

        var inMemory = new InMemoryEventPublisher(NullLogger<InMemoryEventPublisher>.Instance);
        await inMemory.PublishAsync("event.type", "{\"payload\":true}");
        inMemory.PublishedEvents.Should().ContainSingle();
        inMemory.PublishedEvents[0].Payload.Should().Be("{\"payload\":true}");
    }
}

