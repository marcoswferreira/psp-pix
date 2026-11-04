using Domain.Enums;
using Domain.Interfaces.Services;
using FluentAssertions;
using Infrastructure.Services;

namespace Application.UnitTests.Services;

public class BankingProviderTests
{
    private readonly SimulatedBankingProvider _provider = new();

    [Fact]
    public async Task RegisterCharge_DefaultSuccess_ShouldReturnBankTransactionId()
    {
        var request = new BankChargeRequest("TX123", 100m, "12345678901", "Cobrança");
        var result = await _provider.RegisterChargeAsync(request);

        result.Success.Should().BeTrue();
        result.BankTransactionId.Should().NotBeNullOrWhiteSpace();
        result.ErrorCode.Should().BeNull();
    }

    [Fact]
    public async Task RegisterCharge_ChaosFail_ShouldReturnFailure()
    {
        var chaos = new ChaosOptions(ChaosAction.Fail, FailureReason: "Banco fora do ar", BankErrorCode: "ERR_SPI_99");
        var request = new BankChargeRequest("TX123", 100m, "12345678901", "Cobrança", chaos);

        var result = await _provider.RegisterChargeAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("ERR_SPI_99");
        result.ErrorMessage.Should().Be("Banco fora do ar");
    }

    [Fact]
    public async Task SendPayout_DefaultSuccess_ShouldReturnEndToEndIdAndBankTransactionId()
    {
        var request = new BankPayoutRequest(Guid.NewGuid(), 250m, "pix@destino.com", "Payout");
        var result = await _provider.SendPayoutAsync(request);

        result.Success.Should().BeTrue();
        result.EndToEndId.Should().StartWith("E12345678");
        result.BankTransactionId.Should().StartWith("PAYOUT-SPI-");
    }

    [Fact]
    public async Task SendPayout_ChaosFail_ShouldReturnFailureWithoutEndToEndId()
    {
        var chaos = new ChaosOptions(ChaosAction.Fail, FailureReason: "Chave inexistente");
        var request = new BankPayoutRequest(Guid.NewGuid(), 250m, "pix@invalido.com", "Payout", chaos);

        var result = await _provider.SendPayoutAsync(request);

        result.Success.Should().BeFalse();
        result.EndToEndId.Should().BeNull();
        result.ErrorMessage.Should().Be("Chave inexistente");
    }

    [Fact]
    public async Task SimulateInboundPayment_ChaosDuplicate_ShouldFlagAsDuplicated()
    {
        var chaos = new ChaosOptions(ChaosAction.Duplicate);
        var request = new SimulatedInboundPaymentRequest("TX123", 50m, chaos);

        var result = await _provider.SimulateInboundPaymentAsync(request);

        result.Success.Should().BeTrue();
        result.WasDuplicated.Should().BeTrue();
    }

    [Fact]
    public async Task SimulateInboundPayment_ChaosTimeout_ShouldThrowTimeoutException()
    {
        var chaos = new ChaosOptions(ChaosAction.Timeout);
        var request = new SimulatedInboundPaymentRequest("TX123", 50m, chaos);

        var act = async () => await _provider.SimulateInboundPaymentAsync(request);

        await act.Should().ThrowAsync<TimeoutException>();
    }
}
