using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MiniPspPix.Application.Webhooks;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Ledger;
using MiniPspPix.Domain.Outbox;
using MiniPspPix.Domain.Webhooks;
using MiniPspPix.Infrastructure.Ledger;
using MiniPspPix.Infrastructure.Messaging;
using MiniPspPix.Infrastructure.Persistence;
using MiniPspPix.Infrastructure.Webhooks;

namespace MiniPspPix.UnitTests;

public class RepositoriesAndLedgerServiceTests
{
    private static PixDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PixDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new PixDbContext(options);
    }

    [Fact]
    public async Task EfCoreChargeRepository_AddAndGetById_ShouldWork()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreChargeRepository(db);

        var charge = new PixCharge(
            TxId.NewTxId(),
            100m,
            new PixKey("test@pix.com"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            null
        );

        await repo.AddAsync(charge);
        var fetched = await repo.GetByIdAsync(charge.Id);

        fetched.Should().NotBeNull();
        fetched!.TxId.Should().Be(charge.TxId);
    }

    [Fact]
    public async Task EfCoreOutboxRepository_Batch_MarkProcessed_RecordFailure_ShouldWork()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreOutboxRepository(db);

        var msg1 = new OutboxMessage("event.1", "{}");
        var msg2 = new OutboxMessage("event.2", "{}");

        await repo.AddAsync(msg1);
        await repo.AddAsync(msg2);

        var batch = await repo.GetUnprocessedBatchAsync(10);
        batch.Count.Should().Be(2);

        await repo.MarkAsProcessedAsync(msg1.Id);
        await repo.RecordFailureAsync(msg2.Id, "Erro no broker");

        var afterBatch = await repo.GetUnprocessedBatchAsync(10);
        afterBatch.Count.Should().Be(1);
        afterBatch[0].Id.Should().Be(msg2.Id);
        afterBatch[0].RetryCount.Should().Be(1);
        afterBatch[0].LastError.Should().Be("Erro no broker");
    }

    [Fact]
    public async Task EfCoreWebhookRepository_SubscriptionAndDeliveryLifecycle_ShouldWork()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreWebhookRepository(db);

        var sub = new WebhookSubscription("M001", "https://api.merchant.com/wh");
        await repo.AddSubscriptionAsync(sub);

        var fetched = await repo.GetSubscriptionByMerchantIdAsync("M001");
        fetched.Should().NotBeNull();

        var activeSubs = await repo.GetActiveSubscriptionsAsync();
        activeSubs.Should().ContainSingle(s => s.MerchantId == "M001");

        var delivery = new WebhookDelivery(sub.Id, sub.TargetUrl, "pix.paid", "{}", "sig");
        await repo.AddDeliveryAsync(delivery);

        var pending = await repo.GetPendingDeliveriesAsync(10);
        pending.Should().ContainSingle(d => d.Id == delivery.Id);

        delivery.RecordSuccess(200);
        await repo.UpdateDeliveryAsync(delivery);

        var pendingAfter = await repo.GetPendingDeliveriesAsync(10);
        pendingAfter.Should().BeEmpty();
    }

    [Fact]
    public async Task EfCoreLedgerService_GetAccountAndStatement_ShouldWork()
    {
        using var db = CreateDbContext();
        var service = new EfCoreLedgerService(db);

        var acc1 = new LedgerAccount(Guid.NewGuid(), "M01", AccountType.Merchant, 500m);
        var acc2 = new LedgerAccount(Guid.NewGuid(), "T01", AccountType.PixSettlementTransit, 0m);
        db.LedgerAccounts.AddRange(acc1, acc2);
        await db.SaveChangesAsync();

        var fetched = await service.GetAccountAsync(acc1.Id);
        fetched.Should().NotBeNull();
        fetched!.Balance.Should().Be(500m);

        await service.RecordDoubleEntryTransferAsync(acc1.Id, acc2.Id, 100m, "Transfer", "REF1", "Desc");

        var statement = await service.GetStatementAsync(acc1.Id);
        statement.Should().ContainSingle(e => e.Amount == 100m && e.Type == EntryType.Debit);
    }

    [Fact]
    public async Task InMemoryEventPublisher_PublishAsync_ShouldStoreEvent()
    {
        var publisher = new InMemoryEventPublisher(NullLogger<InMemoryEventPublisher>.Instance);
        await publisher.PublishAsync("test.event", "{\"data\":1}");

        publisher.PublishedEvents.Should().ContainSingle();
        publisher.PublishedEvents[0].EventType.Should().Be("test.event");
    }

    [Fact]
    public async Task DispatchWebhookUseCase_WhenClientSucceeds_ShouldMarkSuccess()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreWebhookRepository(db);

        var sub = new WebhookSubscription("M001", "https://valid.com/wh");
        await repo.AddSubscriptionAsync(sub);

        var delivery = new WebhookDelivery(sub.Id, sub.TargetUrl, "event", "{}", "sig");
        await repo.AddDeliveryAsync(delivery);

        var mockClient = new MockWebhookSenderClient((200, null));
        var useCase = new DispatchWebhookUseCase(repo, mockClient);

        var result = await useCase.ExecuteDeliveryAsync(delivery);

        result.Should().BeTrue();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        delivery.LastHttpStatus.Should().Be(200);
    }

    [Fact]
    public async Task DispatchWebhookUseCase_WhenClientFails_ShouldMarkFailure()
    {
        using var db = CreateDbContext();
        var repo = new EfCoreWebhookRepository(db);

        var sub = new WebhookSubscription("M001", "https://valid.com/wh");
        await repo.AddSubscriptionAsync(sub);

        var delivery = new WebhookDelivery(sub.Id, sub.TargetUrl, "event", "{}", "sig");
        await repo.AddDeliveryAsync(delivery);

        var mockClient = new MockWebhookSenderClient((500, "Internal error"));
        var useCase = new DispatchWebhookUseCase(repo, mockClient);

        var result = await useCase.ExecuteDeliveryAsync(delivery);

        result.Should().BeFalse();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.LastHttpStatus.Should().Be(500);
    }

    private class MockWebhookSenderClient : IWebhookSenderClient
    {
        private readonly (int StatusCode, string? Error) _response;
        public MockWebhookSenderClient((int, string?) response) => _response = response;

        public Task<(int StatusCode, string? Error)> SendAsync(string url, string payloadJson, string signature, CancellationToken ct = default)
        {
            return Task.FromResult(_response);
        }
    }
}

