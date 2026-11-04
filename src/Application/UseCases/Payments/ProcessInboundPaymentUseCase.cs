using System.Text.Json;
using Application.UseCases.Payments.Dto;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Domain.ValueObjects;

namespace Application.UseCases.Payments;

public class ProcessInboundPaymentUseCase(
    IChargeRepository chargeRepository,
    ILedgerRepository ledgerRepository,
    IOutboxRepository outboxRepository,
    IBankingProvider bankingProvider,
    Guid pixTransitAccountId,
    Guid merchantAccountId)
{
    private readonly IChargeRepository _chargeRepository = chargeRepository;
    private readonly ILedgerRepository _ledgerRepository = ledgerRepository;
    private readonly IOutboxRepository _outboxRepository = outboxRepository;
    private readonly IBankingProvider _bankingProvider = bankingProvider;
    private readonly Guid _pixTransitAccountId = pixTransitAccountId;
    private readonly Guid _merchantAccountId = merchantAccountId;

    public async Task<InboundPaymentOutput> ExecuteAsync(ProcessInboundPaymentInput input, CancellationToken ct = default)
    {
        var charge = await _chargeRepository.GetByTxIdAsync(input.TxId, ct)
            ?? throw new DomainException($"Cobrança Pix não encontrada para o TxId '{input.TxId}'.");

        // Replay idempotente: se já liquidada, retorna sucesso sem duplicar créditos no Ledger
        if (charge.Status == ChargeStatus.Completed)
        {
            return new InboundPaymentOutput(
                true,
                charge.TxId,
                charge.EndToEndId ?? string.Empty,
                charge.Amount,
                charge.PaidAtUtc ?? DateTime.UtcNow,
                "Cobrança já havia sido liquidada anteriormente (Replay Idempotente)."
            );
        }

        if (charge.Amount != input.Amount)
        {
            throw new DomainException($"Valor divergente: esperado {charge.Amount:C}, recebido {input.Amount:C}.");
        }

        var bankSimResult = await _bankingProvider.SimulateInboundPaymentAsync(
            new SimulatedInboundPaymentRequest(input.TxId, input.Amount, input.Chaos),
            ct
        );

        var endToEndId = !string.IsNullOrWhiteSpace(input.EndToEndId)
            ? new EndToEndId(input.EndToEndId)
            : new EndToEndId(bankSimResult.EndToEndId);

        var paidAtUtc = DateTime.UtcNow;
        charge.MarkAsPaid(endToEndId, paidAtUtc);
        await _chargeRepository.UpdateAsync(charge, ct);

        // Lançamento de Dupla Entrada Contábil
        // DEBIT: Conta Transitória Bacen
        // CREDIT: Conta Disponível do Lojista
        await _ledgerRepository.RecordDoubleEntryTransferAsync(
            sourceAccountId: _pixTransitAccountId,
            destinationAccountId: _merchantAccountId,
            amount: charge.Amount,
            referenceType: "PixInboundPayment",
            referenceId: endToEndId.Value,
            description: $"Liquidação Pix TxId {charge.TxId}",
            ct: ct
        );

        // Transactional Outbox
        var outboxPayload = JsonSerializer.Serialize(new
        {
            EventType = "pix.payment.received",
            ChargeId = charge.Id,
            TxId = charge.TxId,
            EndToEndId = endToEndId.Value,
            Amount = charge.Amount,
            PaidAtUtc = paidAtUtc
        });

        await _outboxRepository.AddAsync(new OutboxMessage("pix.payment.received", outboxPayload), ct);

        if (bankSimResult.WasDuplicated)
        {
            await ExecuteAsync(input with { Chaos = null }, ct);
        }

        return new InboundPaymentOutput(
            true,
            charge.TxId,
            endToEndId.Value,
            charge.Amount,
            paidAtUtc,
            "Pagamento Pix processado e liquidado com sucesso no Ledger."
        );
    }
}

