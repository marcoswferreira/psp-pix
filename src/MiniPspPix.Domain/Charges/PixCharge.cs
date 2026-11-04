using System.Text.RegularExpressions;
using MiniPspPix.Domain.Exceptions;

namespace MiniPspPix.Domain.Charges;

public enum ChargeStatus
{
    Created = 1,
    Active = 2,
    Completed = 3,
    Expired = 4,
    Rejected = 5
}

public enum PixKeyType
{
    Cpf = 1,
    Cnpj = 2,
    Email = 3,
    Phone = 4,
    Random = 5
}

public record PixKey
{
    public string Value { get; }
    public PixKeyType Type { get; }

    public PixKey(string value, PixKeyType? type = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new PixDomainException("Chave Pix não pode ser vazia.");

        Value = value.Trim();
        Type = type ?? DetectType(Value);
    }

    private static PixKeyType DetectType(string value)
    {
        var digitsOnly = Regex.Replace(value, @"\D", "");
        if (value.Contains('@'))
            return PixKeyType.Email;
        if (digitsOnly.Length == 11 && !value.StartsWith("+"))
            return PixKeyType.Cpf;
        if (digitsOnly.Length == 14)
            return PixKeyType.Cnpj;
        if (value.StartsWith("+") || (digitsOnly.Length is >= 12 and <= 13))
            return PixKeyType.Phone;
        if (Guid.TryParse(value, out _))
            return PixKeyType.Random;

        return PixKeyType.Random;
    }

    public override string ToString() => Value;
}

public record TxId
{
    private static readonly Regex AlphanumericRegex = new("^[a-zA-Z0-9]{1,35}$", RegexOptions.Compiled);

    public string Value { get; }

    public TxId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !AlphanumericRegex.IsMatch(value))
            throw new PixDomainException("TxId deve ter entre 1 e 35 caracteres alfanuméricos.");

        Value = value;
    }

    public static TxId NewTxId() => new(Guid.NewGuid().ToString("N"));

    public override string ToString() => Value;
}

public record EndToEndId
{
    public string Value { get; }

    public EndToEndId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 32 || !value.StartsWith("E"))
            throw new PixDomainException("EndToEndId Pix deve ter 32 caracteres iniciando com 'E'.");

        Value = value;
    }

    public static EndToEndId Generate(string ispb = "00000000")
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var randomPart = Guid.NewGuid().ToString("N")[..11];
        return new($"E{ispb.PadLeft(8, '0')[..8]}{timestamp}{randomPart}");
    }

    public override string ToString() => Value;
}

public class PixCharge
{
    public Guid Id { get; private set; }
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
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public string? EndToEndId { get; private set; }

    // Construtor EF Core
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
            throw new PixDomainException("Valor da cobrança Pix deve ser maior que zero.");

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
            throw new PixDomainException("Cobrança já se encontra expirada.");

        if (DateTime.UtcNow > ExpiresAtUtc)
        {
            Status = ChargeStatus.Expired;
            throw new PixDomainException("Cobrança expirou antes do pagamento.");
        }

        Status = ChargeStatus.Completed;
        EndToEndId = endToEndId.Value;
        PaidAtUtc = paidAtUtc;
    }

    public void Expire()
    {
        if (Status == ChargeStatus.Active)
            Status = ChargeStatus.Expired;
    }
}

