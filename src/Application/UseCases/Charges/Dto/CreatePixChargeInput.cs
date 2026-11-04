namespace Application.UseCases.Charges.Dto;

public record CreatePixChargeInput(
    decimal Amount,
    string PixKey,
    string Description,
    string MerchantName = "PSP MERCHANT",
    string MerchantCity = "BRASILIA",
    string? TxId = null,
    TimeSpan? ExpiresIn = null
);

