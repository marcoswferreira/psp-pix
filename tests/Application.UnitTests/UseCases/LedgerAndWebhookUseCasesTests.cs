using Application.UseCases.Ledger;
using Application.UseCases.Webhooks;
using Application.UseCases.Webhooks.Dto;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using FluentAssertions;
using NSubstitute;

namespace Application.UnitTests.UseCases;

public class LedgerAndWebhookUseCasesTests
{
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly IWebhookRepository _webhookRepo = Substitute.For<IWebhookRepository>();
    private readonly IWebhookSenderClient _senderClient = Substitute.For<IWebhookSenderClient>();

    [Fact]
    public async Task GetAccountBalance_WhenAccountExists_ShouldReturnBalanceOutput()
    {
        var accountId = Guid.NewGuid();
        var account = new LedgerAccount(accountId, "ACC-001", AccountType.Merchant, 250m);
        _ledgerRepo.GetAccountAsync(accountId).Returns(account);

        var useCase = new GetAccountBalanceUseCase(_ledgerRepo);
        var output = await useCase.ExecuteAsync(accountId);

        output.Should().NotBeNull();
        output!.Id.Should().Be(accountId);
        output.Balance.Should().Be(250m);
        output.AccountNumber.Should().Be("ACC-001");
    }

    [Fact]
    public async Task GetAccountStatement_ShouldReturnMappedEntries()
    {
        var accountId = Guid.NewGuid();
        var txId = Guid.NewGuid();
        var entries = new List<LedgerEntry>
        {
            new(txId, accountId, EntryType.Credit, 100m, "Depósito"),
            new(txId, accountId, EntryType.Debit, 30m, "Tarifa")
        };

        _ledgerRepo.GetStatementAsync(accountId).Returns(entries);

        var useCase = new GetAccountStatementUseCase(_ledgerRepo);
        var output = await useCase.ExecuteAsync(accountId);

        output.Should().HaveCount(2);
        output[0].Amount.Should().Be(100m);
        output[0].Type.Should().Be(EntryType.Credit);
        output[1].Amount.Should().Be(30m);
        output[1].Type.Should().Be(EntryType.Debit);
    }

    [Fact]
    public async Task RegisterWebhook_ShouldSaveSubscriptionAndReturnOutput()
    {
        var input = new RegisterWebhookInput("MERCHANT-001", "https://api.empresa.com/webhook", "secret123");
        var useCase = new RegisterWebhookUseCase(_webhookRepo);

        var output = await useCase.ExecuteAsync(input);

        output.Should().NotBeNull();
        output.MerchantId.Should().Be("MERCHANT-001");
        output.TargetUrl.Should().Be("https://api.empresa.com/webhook");
        output.SecretKey.Should().Be("secret123");
        output.IsActive.Should().BeTrue();

        await _webhookRepo.Received(1).AddSubscriptionAsync(Arg.Any<WebhookSubscription>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchWebhook_WhenHttp200_ShouldMarkSuccess()
    {
        var delivery = new WebhookDelivery(
            Guid.NewGuid(),
            "https://merchant.com/webhook",
            "pix.settled",
            "{}",
            "signature"
        );

        _senderClient.SendAsync(delivery.TargetUrl, delivery.PayloadJson, delivery.Signature, Arg.Any<CancellationToken>())
            .Returns((200, (string?)null));

        var useCase = new DispatchWebhookUseCase(_webhookRepo, _senderClient);
        var success = await useCase.ExecuteDeliveryAsync(delivery);

        success.Should().BeTrue();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Delivered);
        await _webhookRepo.Received(1).UpdateDeliveryAsync(delivery, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchWebhook_WhenHttp500_ShouldMarkFailureAndRetry()
    {
        var delivery = new WebhookDelivery(
            Guid.NewGuid(),
            "https://merchant.com/webhook",
            "pix.settled",
            "{}",
            "signature"
        );

        _senderClient.SendAsync(delivery.TargetUrl, delivery.PayloadJson, delivery.Signature, Arg.Any<CancellationToken>())
            .Returns((500, "Internal Error"));

        var useCase = new DispatchWebhookUseCase(_webhookRepo, _senderClient);
        var success = await useCase.ExecuteDeliveryAsync(delivery);

        success.Should().BeFalse();
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(1);
        await _webhookRepo.Received(1).UpdateDeliveryAsync(delivery, Arg.Any<CancellationToken>());
    }
}
