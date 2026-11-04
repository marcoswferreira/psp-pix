using Application.UseCases.Charges;
using Application.UseCases.Charges.Dto;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace Application.UnitTests.UseCases;

public class ChargeUseCasesTests
{
    private readonly IChargeRepository _chargeRepo = Substitute.For<IChargeRepository>();
    private readonly IOutboxRepository _outboxRepo = Substitute.For<IOutboxRepository>();
    private readonly IBrCodeService _brCodeService = Substitute.For<IBrCodeService>();
    private readonly CreatePixChargeUseCase _createUseCase;
    private readonly GetPixChargeByTxIdUseCase _getUseCase;

    public ChargeUseCasesTests()
    {
        _brCodeService.GenerateBrCode(Arg.Any<string>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns("00020126...BRCode");
        _brCodeService.GenerateSvg(Arg.Any<string>(), Arg.Any<int>())
            .Returns("<svg>QR</svg>");

        _createUseCase = new CreatePixChargeUseCase(_chargeRepo, _outboxRepo, _brCodeService);
        _getUseCase = new GetPixChargeByTxIdUseCase(_chargeRepo);
    }

    [Fact]
    public async Task CreateCharge_WithValidInput_ShouldPersistChargeAndOutbox()
    {
        var input = new CreatePixChargeInput(
            Amount: 100m,
            PixKey: "12345678901",
            Description: "Compra Loja",
            MerchantName: "Loja ABC",
            MerchantCity: "Sao Paulo",
            TxId: "TX1234567890123456"
        );

        _chargeRepo.GetByTxIdAsync(input.TxId!).Returns((PixCharge?)null);

        var output = await _createUseCase.ExecuteAsync(input);

        output.Should().NotBeNull();
        output.TxId.Should().Be(input.TxId);
        output.Amount.Should().Be(100m);
        output.BrCode.Should().Be("00020126...BRCode");

        await _chargeRepo.Received(1).AddAsync(Arg.Any<PixCharge>(), Arg.Any<CancellationToken>());
        await _outboxRepo.Received(1).AddAsync(Arg.Is<OutboxMessage>(m => m.EventType == "pix.charge.created"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCharge_WithNegativeAmount_ShouldThrowDomainException()
    {
        var input = new CreatePixChargeInput(
            Amount: -50m,
            PixKey: "12345678901",
            Description: "Desc",
            MerchantName: "Merchant",
            MerchantCity: "City"
        );

        var act = async () => await _createUseCase.ExecuteAsync(input);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*maior que zero*");
    }

    [Fact]
    public async Task CreateCharge_WithExistingTxId_ShouldThrowDomainException()
    {
        var existingCharge = new PixCharge(
            new TxId("TX1234567890123456"),
            100m,
            new PixKey("12345678901"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg"
        );

        _chargeRepo.GetByTxIdAsync("TX1234567890123456").Returns(existingCharge);

        var input = new CreatePixChargeInput(
            Amount: 100m,
            PixKey: "12345678901",
            Description: "Desc",
            MerchantName: "Merchant",
            MerchantCity: "City",
            TxId: "TX1234567890123456"
        );

        var act = async () => await _createUseCase.ExecuteAsync(input);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Já existe uma cobrança*");
    }

    [Fact]
    public async Task GetChargeByTxId_WhenFound_ShouldReturnMappedOutput()
    {
        var charge = new PixCharge(
            new TxId("TX1234567890123456"),
            75.50m,
            new PixKey("teste@pix.com"),
            "Desc",
            "Loja",
            "SP",
            "brcode",
            "svg"
        );

        _chargeRepo.GetByTxIdAsync("TX1234567890123456").Returns(charge);

        var output = await _getUseCase.ExecuteAsync("TX1234567890123456");

        output.Should().NotBeNull();
        output!.TxId.Should().Be("TX1234567890123456");
        output.Amount.Should().Be(75.50m);
    }

    [Fact]
    public async Task GetChargeByTxId_WhenNotFound_ShouldReturnNull()
    {
        _chargeRepo.GetByTxIdAsync("NONEXISTENT").Returns((PixCharge?)null);

        var output = await _getUseCase.ExecuteAsync("NONEXISTENT");

        output.Should().BeNull();
    }
}

