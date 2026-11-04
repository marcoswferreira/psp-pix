using Application.UseCases.Payments;
using Application.UseCases.Payments.Dto;
using Application.UseCases.Payout;
using Application.UseCases.Payout.Dto;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Application.UnitTests.UseCases;

public class PaymentAndPayoutUseCasesTests
{
    private readonly IChargeRepository _chargeRepo = Substitute.For<IChargeRepository>();
    private readonly ILedgerRepository _ledgerRepo = Substitute.For<ILedgerRepository>();
    private readonly IOutboxRepository _outboxRepo = Substitute.For<IOutboxRepository>();
    private readonly IBankingProvider _bankingProvider = Substitute.For<IBankingProvider>();

    private readonly Guid _transitAccountId = Guid.NewGuid();
    private readonly Guid _merchantAccountId = Guid.NewGuid();

    private readonly ProcessInboundPaymentUseCase _paymentUseCase;
    private readonly RequestPayoutUseCase _payoutUseCase;

    public PaymentAndPayoutUseCasesTests()
    {
        _paymentUseCase = new ProcessInboundPaymentUseCase(
            _chargeRepo,
            _ledgerRepo,
            _outboxRepo,
            _bankingProvider,
            _transitAccountId,
            _merchantAccountId
        );

        _payoutUseCase = new RequestPayoutUseCase(
            _ledgerRepo,
            _bankingProvider,
            _outboxRepo,
            _transitAccountId
        );
    }

    [Fact]
    public async Task ProcessInboundPayment_WhenValid_ShouldMarkChargePaidAndTransferLedger()
    {
        var txId = "TX1234567890123456";
        var charge = new PixCharge(
            new TxId(txId),
            100m,
            new PixKey("12345678901"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg"
        );

        _chargeRepo.GetByTxIdAsync(txId).Returns(charge);
        _bankingProvider.SimulateInboundPaymentAsync(Arg.Any<SimulatedInboundPaymentRequest>())
            .Returns(new SimulatedInboundPaymentResult(true, "E1234567820261104120000000000001", "BANK123", false, null));

        var input = new ProcessInboundPaymentInput(txId, 100m);
        var output = await _paymentUseCase.ExecuteAsync(input);

        output.Success.Should().BeTrue();
        output.TxId.Should().Be(txId);
        output.Amount.Should().Be(100m);

        await _chargeRepo.Received(1).UpdateAsync(charge, Arg.Any<CancellationToken>());
        await _ledgerRepo.Received(1).RecordDoubleEntryTransferAsync(
            _transitAccountId,
            _merchantAccountId,
            100m,
            "PixInboundPayment",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
        await _outboxRepo.Received(1).AddAsync(Arg.Is<OutboxMessage>(m => m.EventType == "pix.payment.received"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessInboundPayment_WhenChargeNotFound_ShouldThrowDomainException()
    {
        _chargeRepo.GetByTxIdAsync("UNKNOWN").Returns((PixCharge?)null);

        var input = new ProcessInboundPaymentInput("UNKNOWN", 100m);
        var act = async () => await _paymentUseCase.ExecuteAsync(input);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*não encontrada*");
    }

    [Fact]
    public async Task ProcessInboundPayment_WhenAlreadyPaid_ShouldReturnIdempotentSuccess()
    {
        var txId = "TX1234567890123456";
        var charge = new PixCharge(
            new TxId(txId),
            100m,
            new PixKey("12345678901"),
            "Desc",
            "Merchant",
            "City",
            "brcode",
            "svg"
        );
        charge.MarkAsPaid(new EndToEndId("E1234567820261104120000000000001"), DateTime.UtcNow);

        _chargeRepo.GetByTxIdAsync(txId).Returns(charge);

        var input = new ProcessInboundPaymentInput(txId, 100m);
        var output = await _paymentUseCase.ExecuteAsync(input);

        output.Success.Should().BeTrue();
        output.Message.Should().Contain("liquidada");
        await _ledgerRepo.DidNotReceive().RecordDoubleEntryTransferAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestPayout_WhenValid_ShouldDebitMerchantAndCreditTransit()
    {
        var input = new RequestPayoutInput(
            MerchantAccountId: _merchantAccountId,
            Amount: 150m,
            DestinationPixKey: "destino@pix.com",
            Description: "Saque"
        );

        _ledgerRepo.GetBalanceAsync(_merchantAccountId).Returns(350m);
        _bankingProvider.SendPayoutAsync(Arg.Any<BankPayoutRequest>())
            .Returns(new BankPayoutResult(true, "E1234567820261104120000000000001", "PAYOUT123", null, null, DateTime.UtcNow));

        var output = await _payoutUseCase.ExecuteAsync(input);

        output.Success.Should().BeTrue();
        output.Amount.Should().Be(150m);

        await _ledgerRepo.Received(1).RecordDoubleEntryTransferAsync(
            _merchantAccountId,
            _transitAccountId,
            150m,
            "PixPayout",
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
        await _outboxRepo.Received(1).AddAsync(Arg.Is<OutboxMessage>(m => m.EventType == "pix.payout.completed"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestPayout_WhenInsufficientFunds_ShouldThrowInsufficientFundsException()
    {
        var input = new RequestPayoutInput(
            MerchantAccountId: _merchantAccountId,
            Amount: 1000m,
            DestinationPixKey: "destino@pix.com",
            Description: "Saque maior que saldo"
        );

        _ledgerRepo.RecordDoubleEntryTransferAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()
        ).ThrowsAsync(new InsufficientFundsException("Saldo insuficiente na conta"));

        var act = async () => await _payoutUseCase.ExecuteAsync(input);

        await act.Should().ThrowAsync<InsufficientFundsException>().WithMessage("*Saldo insuficiente*");
    }
}
