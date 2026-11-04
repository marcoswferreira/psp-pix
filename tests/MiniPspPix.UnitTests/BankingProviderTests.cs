using FluentAssertions;
using MiniPspPix.Domain.Banking;
using MiniPspPix.Infrastructure.Banking;

namespace MiniPspPix.UnitTests;

public class BankingProviderTests
{
    private readonly SimulatedBankingProvider _provider = new();

    [Fact]
    public async Task RegisterChargeAsync_Normal_ShouldSucceed()
    {
        var request = new BankChargeRequest("TX123", 100m, "pix@test.com", "Desc");
        var result = await _provider.RegisterChargeAsync(request);

        result.Success.Should().BeTrue();
        result.BankTransactionId.Should().StartWith("BANK-TX-");
    }

    [Fact]
    public async Task RegisterChargeAsync_WithFailureChaos_ShouldFail()
    {
        var chaos = new ChaosOptions(ChaosAction.Fail, FailureReason: "Banco fora do ar", BankErrorCode: "CH99");
        var request = new BankChargeRequest("TX123", 100m, "pix@test.com", "Desc", chaos);

        var result = await _provider.RegisterChargeAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("CH99");
        result.ErrorMessage.Should().Be("Banco fora do ar");
    }

    [Fact]
    public async Task SendPayoutAsync_Normal_ShouldSucceed()
    {
        var request = new BankPayoutRequest(Guid.NewGuid(), 50m, "dest@pix.com", "Payout");
        var result = await _provider.SendPayoutAsync(request);

        result.Success.Should().BeTrue();
        result.EndToEndId.Should().StartWith("E");
        result.BankTransactionId.Should().StartWith("PAYOUT-SPI-");
    }

    [Fact]
    public async Task SendPayoutAsync_WithFailureChaos_ShouldFail()
    {
        var chaos = new ChaosOptions(ChaosAction.Fail, BankErrorCode: "AB03", FailureReason: "Chave inexistente");
        var request = new BankPayoutRequest(Guid.NewGuid(), 50m, "dest@pix.com", "Payout", chaos);

        var result = await _provider.SendPayoutAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("AB03");
        result.ErrorMessage.Should().Be("Chave inexistente");
    }

    [Fact]
    public async Task SimulateInboundPaymentAsync_WithDuplicateChaos_ShouldSetWasDuplicated()
    {
        var chaos = new ChaosOptions(ChaosAction.Duplicate);
        var request = new SimulatedInboundPaymentRequest("TX123", 100m, chaos);

        var result = await _provider.SimulateInboundPaymentAsync(request);

        result.Success.Should().BeTrue();
        result.WasDuplicated.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyChaos_WithTimeout_ShouldThrowTimeoutException()
    {
        var chaos = new ChaosOptions(ChaosAction.Timeout);
        var request = new BankPayoutRequest(Guid.NewGuid(), 10m, "dest@pix.com", "Payout", chaos);

        var act = () => _provider.SendPayoutAsync(request);

        await act.Should().ThrowAsync<TimeoutException>().WithMessage("*Timeout bancário simulado*");
    }

    [Fact]
    public async Task ApplyChaos_WithDelay_ShouldWait()
    {
        var chaos = new ChaosOptions(ChaosAction.Delay, DelayMilliseconds: 50);
        var request = new BankPayoutRequest(Guid.NewGuid(), 10m, "dest@pix.com", "Payout", chaos);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _provider.SendPayoutAsync(request);
        stopwatch.Stop();

        result.Success.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(40);
    }
}

