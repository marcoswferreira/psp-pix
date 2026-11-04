using MiniPspPix.Application.BrCode;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;
using MiniPspPix.Domain.Outbox;

namespace MiniPspPix.Application.Charges;

public record CreateChargeCommand(
    decimal Amount,
    string PixKey,
    string Description,
    string MerchantName = "PSP MERCHANT",
    string MerchantCity = "BRASILIA",
    string? TxId = null,
    TimeSpan? ExpiresIn = null
);

public record ChargeResultDto(
    Guid Id,
    string TxId,
    decimal Amount,
    string Status,
    string PixKey,
    string Description,
    string BrCode,
    string? QrCodeSvg,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc,
    DateTime? PaidAtUtc,
    string? EndToEndId
);

public interface IChargeRepository
{
    Task AddAsync(PixCharge charge, CancellationToken ct = default);
    Task<PixCharge?> GetByTxIdAsync(string txId, CancellationToken ct = default);
    Task<PixCharge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(PixCharge charge, CancellationToken ct = default);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 20, CancellationToken ct = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task RecordFailureAsync(Guid messageId, string error, CancellationToken ct = default);
}

public class CreateChargeUseCase
{
    private readonly IChargeRepository _chargeRepository;
    private readonly IOutboxRepository _outboxRepository;

    public CreateChargeUseCase(IChargeRepository chargeRepository, IOutboxRepository outboxRepository)
    {
        _chargeRepository = chargeRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<ChargeResultDto> ExecuteAsync(CreateChargeCommand command, CancellationToken ct = default)
    {
        if (command.Amount <= 0)
            throw new PixDomainException("O valor da cobrança Pix deve ser maior que zero.");

        var pixKey = new PixKey(command.PixKey);
        var txId = string.IsNullOrWhiteSpace(command.TxId) ? TxId.NewTxId() : new TxId(command.TxId);

        var existing = await _chargeRepository.GetByTxIdAsync(txId.Value, ct);
        if (existing != null)
            throw new PixDomainException($"Já existe uma cobrança com o TxId '{txId.Value}'.");

        var brCode = new BrCodeBuilder()
            .WithPixKey(pixKey.Value)
            .WithMerchant(command.MerchantName, command.MerchantCity)
            .WithAmount(command.Amount)
            .WithTxId(txId.Value)
            .WithDescription(command.Description)
            .Build();

        var qrCodeSvg = QrCodeSvgGenerator.GenerateSvg(brCode);

        var charge = new PixCharge(
            txId,
            command.Amount,
            pixKey,
            command.Description,
            command.MerchantName,
            command.MerchantCity,
            brCode,
            qrCodeSvg,
            command.ExpiresIn
        );

        await _chargeRepository.AddAsync(charge, ct);

        // Transactional Outbox Event
        var outboxPayload = System.Text.Json.JsonSerializer.Serialize(new
        {
            EventType = "pix.charge.created",
            ChargeId = charge.Id,
            TxId = charge.TxId,
            Amount = charge.Amount,
            CreatedAtUtc = charge.CreatedAtUtc
        });

        await _outboxRepository.AddAsync(new OutboxMessage("pix.charge.created", outboxPayload), ct);

        return ToDto(charge);
    }

    public static ChargeResultDto ToDto(PixCharge charge) => new(
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

