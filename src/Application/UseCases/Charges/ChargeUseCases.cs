using System.Text.Json;
using Application.UseCases.Charges.Dto;
using Domain.Common.Exceptions;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Domain.ValueObjects;

namespace Application.UseCases.Charges;

public class CreatePixChargeUseCase(
    IChargeRepository chargeRepository,
    IOutboxRepository outboxRepository,
    IBrCodeService brCodeService)
{
    private readonly IChargeRepository _chargeRepository = chargeRepository;
    private readonly IOutboxRepository _outboxRepository = outboxRepository;
    private readonly IBrCodeService _brCodeService = brCodeService;

    public async Task<PixChargeOutput> ExecuteAsync(CreatePixChargeInput input, CancellationToken ct = default)
    {
        if (input.Amount <= 0)
            throw new DomainException("O valor da cobrança Pix deve ser maior que zero.");

        var pixKey = new PixKey(input.PixKey);
        var txId = string.IsNullOrWhiteSpace(input.TxId) ? TxId.NewTxId() : new TxId(input.TxId);

        var existing = await _chargeRepository.GetByTxIdAsync(txId.Value, ct);
        if (existing != null)
            throw new DomainException($"Já existe uma cobrança com o TxId '{txId.Value}'.");

        var brCode = _brCodeService.GenerateBrCode(
            pixKey.Value,
            input.Amount,
            input.MerchantName,
            input.MerchantCity,
            txId.Value,
            input.Description
        );

        var qrCodeSvg = _brCodeService.GenerateSvg(brCode);

        var charge = new PixCharge(
            txId,
            input.Amount,
            pixKey,
            input.Description,
            input.MerchantName,
            input.MerchantCity,
            brCode,
            qrCodeSvg,
            input.ExpiresIn
        );

        await _chargeRepository.AddAsync(charge, ct);

        // Transactional Outbox Event
        var outboxPayload = JsonSerializer.Serialize(new
        {
            EventType = "pix.charge.created",
            ChargeId = charge.Id,
            TxId = charge.TxId,
            Amount = charge.Amount,
            CreatedAtUtc = charge.CreatedAtUtc
        });

        await _outboxRepository.AddAsync(new OutboxMessage("pix.charge.created", outboxPayload), ct);

        return ToOutput(charge);
    }

    public static PixChargeOutput ToOutput(PixCharge charge) => new(
        charge.Id,
        charge.TxId,
        charge.Amount,
        charge.Status.ToString(),
        charge.PixKeyValue,
        charge.Description,
        charge.BrCode,
        charge.QrCodeSvg,
        charge.CreatedAtUtc,
        charge.ExpiresAtUtc,
        charge.PaidAtUtc,
        charge.EndToEndId
    );
}

public class GetPixChargeByTxIdUseCase(IChargeRepository chargeRepository)
{
    private readonly IChargeRepository _chargeRepository = chargeRepository;

    public async Task<PixChargeOutput?> ExecuteAsync(string txId, CancellationToken ct = default)
    {
        var charge = await _chargeRepository.GetByTxIdAsync(txId, ct);
        return charge != null ? CreatePixChargeUseCase.ToOutput(charge) : null;
    }
}

