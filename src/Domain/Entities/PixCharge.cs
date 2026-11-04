using Domain.Common.Entities;
using Domain.Common.Exceptions;
using Domain.Enums;
using Domain.ValueObjects;

namespace Domain.Entities;

public class PixCharge : BaseEntity, IAggregateRoot
{
    public string TxId { get; private set; }
    public decimal Amount { get; private set; }
    public ChargeStatus Status { get; private set; }
    public string PixKeyValue { get; private set; }
    public PixKeyType PixKeyType { get; private set; }
    public string Description { get; private set; }
    public string MerchantName { get; private set; }
    public string MerchantCity { get; private set; }
    public string BrCode { get; private set; }
    public string? QrCodeSvg { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? EndToEndId { get; private set; }

    protected PixCharge()
    {
        TxId = null!;
        PixKeyValue = null!;
        Description = null!;
        MerchantName = null!;
        MerchantCity = null!;
        BrCode = null!;
    }

    public PixCharge(
        TxId txId,
        decimal amount,
        PixKey pixKey,
        string description,
        string merchantName,
        string merchantCity,
        string brCode,
        string? qrCodeSvg,
        TimeSpan? expiresIn = null)
    {
        if (amount <= 0)
            throw new DomainException("Valor da cobrança Pix deve ser maior que zero.");

        Id = Guid.NewGuid();
        TxId = txId.Value;
        Amount = amount;
        Status = ChargeStatus.Active;
        PixKeyValue = pixKey.Value;
        PixKeyType = pixKey.Type;
        Description = description;
        MerchantName = merchantName;
        MerchantCity = merchantCity;
        BrCode = brCode;
        QrCodeSvg = qrCodeSvg;
        CreatedAtUtc = DateTime.UtcNow;
        ExpiresAtUtc = CreatedAtUtc.Add(expiresIn ?? TimeSpan.FromDays(1));
    }

    public void MarkAsPaid(EndToEndId endToEndId, DateTime paidAtUtc)
    {
        if (Status == ChargeStatus.Completed)
            return; // Idempotente

        if (Status == ChargeStatus.Expired)
            throw new DomainException("Cobrança já se encontra expirada.");

        if (DateTime.UtcNow > ExpiresAtUtc)
        {
            Status = ChargeStatus.Expired;
            throw new DomainException("Cobrança expirou antes do pagamento.");
        }

        Status = ChargeStatus.Completed;
        EndToEndId = endToEndId.Value;
        PaidAtUtc = paidAtUtc;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Expire()
    {
        if (Status == ChargeStatus.Active)
        {
            Status = ChargeStatus.Expired;
            UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}

