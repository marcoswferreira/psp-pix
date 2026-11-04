using MiniPspPix.Application.Charges;
using MiniPspPix.Application.Ledger;
using MiniPspPix.Domain.Banking;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Outbox;

namespace MiniPspPix.Application.Payments;

public record ProcessInboundPaymentCommand(
    string TxId,
    decimal Amount,
    string? EndToEndId = null,
    ChaosOptions? Chaos = null
);

public record InboundPaymentResultDto(
    bool Success,
    string TxId,
    string EndToEndId,
    decimal Amount,
    DateTime PaidAtUtc,
    string Message
);

public record RequestPayoutCommand(
    Guid MerchantAccountId,
    decimal Amount,
    string DestinationPixKey,
    string Description,
    ChaosOptions? Chaos = null
);

public record PayoutResultDto(
    bool Success,
    Guid PayoutId,
    string? EndToEndId,
    decimal Amount,
    decimal RemainingBalance,
    string Message
);

public class ProcessInboundPaymentUseCase
{
    private readonly IChargeRepository _chargeRepository;
    private readonly ILedgerService _ledgerService;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IBankingProvider _bankingProvider;
    private readonly Guid _pixTransitAccountId;
    private readonly Guid _merchantAccountId;

    public ProcessInboundPaymentUseCase(
        IChargeRepository chargeRepository,
        ILedgerService ledgerService,
        IOutboxRepository outboxRepository,
        IBankingProvider bankingProvider,
        Guid pixTransitAccountId,
        Guid merchantAccountId)
    {
        _chargeRepository = chargeRepository;
        _ledgerService = ledgerService;
        _outboxRepository = outboxRepository;
        _bankingProvider = bankingProvider;
        _pixTransitAccountId = pixTransitAccountId;
        _merchantAccountId = merchantAccountId;
    }

    public async Task<InboundPaymentResultDto> ExecuteAsync(ProcessInboundPaymentCommand command, CancellationToken ct = default)
    {
        var charge = await _chargeRepository.GetByTxIdAsync(command.TxId, ct)
            ?? throw new PixDomainException($"Cobrança Pix não encontrada para o TxId '{command.TxId}'.");

        // Idempotência: se já foi paga, retorna sucesso sem duplicar créditos no Ledger
        if (charge.Status == ChargeStatus.Completed)
        {
            return new InboundPaymentResultDto(
                true,
                charge.TxId,
                charge.EndToEndId ?? string.Empty,
                charge.Amount,
                charge.PaidAtUtc ?? DateTime.UtcNow,
                "Cobrança já havia sido liquidada anteriormente (Replay Idempotente)."
            );
        }

        if (charge.Amount != command.Amount)
        {
            throw new PixDomainException($"Valor divergente: esperado {charge.Amount:C}, recebido {command.Amount:C}.");
        }

        // Simula ou invoca o provedor bancário com suporte a injeção de caos
        var bankSimResult = await _bankingProvider.SimulateInboundPaymentAsync(
            new SimulatedInboundPaymentRequest(command.TxId, command.Amount, command.Chaos),
            ct
        );

        var endToEndId = !string.IsNullOrWhiteSpace(command.EndToEndId)
            ? new EndToEndId(command.EndToEndId)
            : new EndToEndId(bankSimResult.EndToEndId);

        var paidAtUtc = DateTime.UtcNow;
        charge.MarkAsPaid(endToEndId, paidAtUtc);
        await _chargeRepository.UpdateAsync(charge, ct);

        // Lançamento Contábil de Dupla Entrada:
        // DEBIT: Conta Transitória Bacen (PixSettlementTransit)
        // CREDIT: Conta Disponível do Lojista (MerchantAccount)
        await _ledgerService.RecordDoubleEntryTransferAsync(
            sourceAccountId: _pixTransitAccountId,
            destinationAccountId: _merchantAccountId,
            amount: charge.Amount,
            referenceType: "PixInboundPayment",
            referenceId: endToEndId.Value,
            description: $"Liquidação Pix TxId {charge.TxId}",
            ct: ct
        );

        // Gravação no Transactional Outbox
        var outboxPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            EventType = "pix.payment.received",
            ChargeId = charge.Id,
            TxId = charge.TxId,
            EndToEndId = endToEndId.Value,
            Amount = charge.Amount,
            PaidAtUtc = paidAtUtc
        });

        await _outboxRepository.AddAsync(new OutboxMessage("pix.payment.received", outboxPayload), ct);

        // Se o caos indicou duplicidade de notificação bancária, simulamos uma segunda chamada imediata
        if (bankSimResult.WasDuplicated)
        {
            // A segunda tentativa chamará o mesmo método e deverá ser absorvida de forma idempotente sem alterar saldos
            await ExecuteAsync(command with { Chaos = null }, ct);
        }

        return new InboundPaymentResultDto(
            true,
            charge.TxId,
            endToEndId.Value,
            charge.Amount,
            paidAtUtc,
            "Pagamento Pix processado e liquidado com sucesso no Ledger."
        );
    }
}

public class RequestPayoutUseCase
{
    private readonly ILedgerService _ledgerService;
    private readonly IBankingProvider _bankingProvider;
    private readonly IOutboxRepository _outboxRepository;
    private readonly Guid _pixTransitAccountId;

    public RequestPayoutUseCase(
        ILedgerService ledgerService,
        IBankingProvider bankingProvider,
        IOutboxRepository outboxRepository,
        Guid pixTransitAccountId)
    {
        _ledgerService = ledgerService;
        _bankingProvider = bankingProvider;
        _outboxRepository = outboxRepository;
        _pixTransitAccountId = pixTransitAccountId;
    }

    public async Task<PayoutResultDto> ExecuteAsync(RequestPayoutCommand command, CancellationToken ct = default)
    {
        if (command.Amount <= 0)
            throw new PixDomainException("Valor de saque/payout deve ser maior que zero.");

        var payoutId = Guid.NewGuid();

        // Débito no saldo do lojista e crédito na conta de trânsito bancário com controle de concorrência
        await _ledgerService.RecordDoubleEntryTransferAsync(
            sourceAccountId: command.MerchantAccountId,
            destinationAccountId: _pixTransitAccountId,
            amount: command.Amount,
            referenceType: "PixPayout",
            referenceId: payoutId.ToString(),
            description: $"Payout Pix para {command.DestinationPixKey}: {command.Description}",
            ct: ct
        );

        // Chamada ao Provedor Bancário (com suporte a injeção de caos / timeout / failure)
        var bankResult = await _bankingProvider.SendPayoutAsync(
            new BankPayoutRequest(payoutId, command.Amount, command.DestinationPixKey, command.Description, command.Chaos),
            ct
        );

        var remainingBalance = await _ledgerService.GetBalanceAsync(command.MerchantAccountId, ct);

        // Transactional Outbox
        var outboxPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            EventType = "pix.payout.completed",
            PayoutId = payoutId,
            EndToEndId = bankResult.EndToEndId,
            Amount = command.Amount,
            DestinationPixKey = command.DestinationPixKey,
            SettledAtUtc = bankResult.SettledAtUtc
        });

        await _outboxRepository.AddAsync(new OutboxMessage("pix.payout.completed", outboxPayload), ct);

        return new PayoutResultDto(
            bankResult.Success,
            payoutId,
            bankResult.EndToEndId,
            command.Amount,
            remainingBalance,
            bankResult.Success ? "Payout Pix concluído com sucesso." : $"Payout pendente ou rejeitado: {bankResult.ErrorMessage}"
        );
    }
}

