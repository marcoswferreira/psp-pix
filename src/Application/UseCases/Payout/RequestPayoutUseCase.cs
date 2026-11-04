using System.Text.Json;
using Application.UseCases.Payout.Dto;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;

namespace Application.UseCases.Payout;

public class RequestPayoutUseCase(
    ILedgerRepository ledgerRepository,
    IBankingProvider bankingProvider,
    IOutboxRepository outboxRepository,
    Guid pixTransitAccountId)
{
    private readonly ILedgerRepository _ledgerRepository = ledgerRepository;
    private readonly IBankingProvider _bankingProvider = bankingProvider;
    private readonly IOutboxRepository _outboxRepository = outboxRepository;
    private readonly Guid _pixTransitAccountId = pixTransitAccountId;

    public async Task<PayoutOutput> ExecuteAsync(RequestPayoutInput input, CancellationToken ct = default)
    {
        if (input.Amount <= 0)
            throw new DomainException("Valor de saque/payout deve ser maior que zero.");

        var payoutId = Guid.NewGuid();

        // Débito no saldo do lojista e crédito na conta de trânsito bancário com controle de concorrência
        await _ledgerRepository.RecordDoubleEntryTransferAsync(
            sourceAccountId: input.MerchantAccountId,
            destinationAccountId: _pixTransitAccountId,
            amount: input.Amount,
            referenceType: "PixPayout",
            referenceId: payoutId.ToString(),
            description: $"Payout Pix para {input.DestinationPixKey}: {input.Description}",
            ct: ct
        );

        // Chamada ao Provedor Bancário (com suporte a injeção de caos / timeout / failure)
        var bankResult = await _bankingProvider.SendPayoutAsync(
            new BankPayoutRequest(payoutId, input.Amount, input.DestinationPixKey, input.Description, input.Chaos),
            ct
        );

        var remainingBalance = await _ledgerRepository.GetBalanceAsync(input.MerchantAccountId, ct);

        // Transactional Outbox
        var outboxPayload = JsonSerializer.Serialize(new
        {
            EventType = "pix.payout.completed",
            PayoutId = payoutId,
            EndToEndId = bankResult.EndToEndId,
            Amount = input.Amount,
            DestinationPixKey = input.DestinationPixKey,
            SettledAtUtc = bankResult.SettledAtUtc
        });

        await _outboxRepository.AddAsync(new OutboxMessage("pix.payout.completed", outboxPayload), ct);

        return new PayoutOutput(
            bankResult.Success,
            payoutId,
            bankResult.EndToEndId,
            input.Amount,
            remainingBalance,
            bankResult.Success ? "Payout Pix concluído com sucesso." : $"Payout pendente ou rejeitado: {bankResult.ErrorMessage}"
        );
    }
}
