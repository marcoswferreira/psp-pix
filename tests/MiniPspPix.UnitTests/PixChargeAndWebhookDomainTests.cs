using FluentAssertions;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Webhooks;

namespace MiniPspPix.UnitTests;

public class PixChargeTests
{
    [Fact]
    public void NewPixCharge_ShouldBeCreatedActive()
    {
        var pixKey = new PixKey("12345678909");
        var txId = TxId.NewTxId();

        var charge = new PixCharge(
            txId,
            100.00m,
            pixKey,
            "Teste",
            "Recebedor",
            "Brasilia",
            "brcode-fake",
            "<svg></svg>"
        );

        charge.Status.Should().Be(ChargeStatus.Active);
        charge.Amount.Should().Be(100.00m);
        charge.PaidAtUtc.Should().BeNull();
        charge.EndToEndId.Should().BeNull();
    }

    [Fact]
    public void MarkAsPaid_ShouldUpdateStatusAndBeIdempotent()
    {
        var pixKey = new PixKey("user@email.com");
        var txId = TxId.NewTxId();
        var charge = new PixCharge(txId, 50m, pixKey, "Ref", "Merch", "City", "brcode", null);

        var e2e = EndToEndId.Generate();
        var paidAt = DateTime.UtcNow;

        charge.MarkAsPaid(e2e, paidAt);

        charge.Status.Should().Be(ChargeStatus.Completed);
        charge.EndToEndId.Should().Be(e2e.Value);

        // Segunda chamada idêntica não deve lançar erro
        charge.MarkAsPaid(e2e, paidAt);
        charge.Status.Should().Be(ChargeStatus.Completed);
    }

    [Fact]
    public void MarkAsPaid_WhenExpired_ShouldThrowDomainException()
    {
        var pixKey = new PixKey("user@email.com");
        var txId = TxId.NewTxId();
        var charge = new PixCharge(txId, 50m, pixKey, "Ref", "Merch", "City", "brcode", null, TimeSpan.FromMilliseconds(-100));

        var act = () => charge.MarkAsPaid(EndToEndId.Generate(), DateTime.UtcNow);

        act.Should().Throw<PixDomainException>().WithMessage("*expir*");
    }
}

public class WebhookDomainTests
{
    [Fact]
    public void RecordFailure_ShouldCalculateExponentialBackoffUntilMaxRetries()
    {
        var delivery = new WebhookDelivery(
            Guid.NewGuid(),
            "https://merchant.com/webhook",
            "pix.payment.received",
            "{}",
            "signature"
        );

        delivery.Status.Should().Be(WebhookDeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(0);

        // Tentativa 1 -> 2^0 * 2 = 2s
        delivery.RecordFailure(500, "Internal Server Error");
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);
        delivery.AttemptCount.Should().Be(1);
        delivery.NextRetryAtUtc.Should().NotBeNull();

        // Tentativa 2, 3, 4
        delivery.RecordFailure(502, "Bad Gateway");
        delivery.RecordFailure(503, "Service Unavailable");
        delivery.RecordFailure(504, "Gateway Timeout");
        delivery.AttemptCount.Should().Be(4);
        delivery.Status.Should().Be(WebhookDeliveryStatus.Failed);

        // Tentativa 5 (esgota maxRetries) -> DeadLettered
        delivery.RecordFailure(500, "Fatal Error", maxRetries: 5);
        delivery.AttemptCount.Should().Be(5);
        delivery.Status.Should().Be(WebhookDeliveryStatus.DeadLettered);
        delivery.NextRetryAtUtc.Should().BeNull();
    }
}

