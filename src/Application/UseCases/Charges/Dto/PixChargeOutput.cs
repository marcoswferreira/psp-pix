namespace Application.UseCases.Charges.Dto;

public record PixChargeOutput(
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

