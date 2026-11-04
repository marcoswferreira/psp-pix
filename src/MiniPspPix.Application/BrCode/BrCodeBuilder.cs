using System.Globalization;
using System.Text;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Exceptions;

namespace MiniPspPix.Application.BrCode;

public static class Crc16Ccitt
{
    private const ushort Polynomial = 0x1021;
    private const ushort InitialValue = 0xFFFF;

    public static string ComputeHex(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        ushort crc = InitialValue;

        foreach (var b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                {
                    crc = (ushort)((crc << 1) ^ Polynomial);
                }
                else
                {
                    crc = (ushort)(crc << 1);
                }
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }
}

public class BrCodeBuilder
{
    private string _pixKey = string.Empty;
    private string _merchantName = "PSP MERCHANT";
    private string _merchantCity = "BRASILIA";
    private decimal _amount;
    private string _txId = "***";
    private string? _description;
    private bool _isDynamic;

    public BrCodeBuilder WithPixKey(string pixKey)
    {
        _pixKey = pixKey.Trim();
        return this;
    }

    public BrCodeBuilder WithMerchant(string name, string city)
    {
        _merchantName = Sanitize(name, 25);
        _merchantCity = Sanitize(city, 15);
        return this;
    }

    public BrCodeBuilder WithAmount(decimal amount)
    {
        if (amount < 0)
            throw new PixDomainException("Valor do Pix não pode ser negativo.");

        _amount = amount;
        return this;
    }

    public BrCodeBuilder WithTxId(string txId)
    {
        _txId = string.IsNullOrWhiteSpace(txId) ? "***" : txId.Trim();
        return this;
    }

    public BrCodeBuilder WithDescription(string? description)
    {
        _description = string.IsNullOrWhiteSpace(description) ? null : Sanitize(description, 50);
        return this;
    }

    public BrCodeBuilder AsDynamic(bool isDynamic = true)
    {
        _isDynamic = isDynamic;
        return this;
    }

    public string Build()
    {
        if (string.IsNullOrWhiteSpace(_pixKey))
            throw new PixDomainException("Chave Pix é obrigatória para o BR Code.");

        var sb = new StringBuilder();

        // 00: Payload Format Indicator
        AppendEmvTag(sb, "00", "01");

        // 01: Point of Initiation Method ("11" estático, "12" dinâmico)
        AppendEmvTag(sb, "01", _isDynamic ? "12" : "11");

        // 26: Merchant Account Information
        var merchantAccountInfo = new StringBuilder();
        AppendEmvTag(merchantAccountInfo, "00", "br.gov.bcb.pix");
        AppendEmvTag(merchantAccountInfo, "01", _pixKey);
        if (!string.IsNullOrEmpty(_description))
        {
            AppendEmvTag(merchantAccountInfo, "02", _description);
        }
        AppendEmvTag(sb, "26", merchantAccountInfo.ToString());

        // 52: Merchant Category Code (fixo "0000" para Bacen Pix)
        AppendEmvTag(sb, "52", "0000");

        // 53: Transaction Currency ("986" para Real Brasileiro - ISO 4217)
        AppendEmvTag(sb, "53", "986");

        // 54: Transaction Amount
        if (_amount > 0)
        {
            var formattedAmount = _amount.ToString("F2", CultureInfo.InvariantCulture);
            AppendEmvTag(sb, "54", formattedAmount);
        }

        // 58: Country Code ("BR")
        AppendEmvTag(sb, "58", "BR");

        // 59: Merchant Name (máx 25)
        AppendEmvTag(sb, "59", string.IsNullOrWhiteSpace(_merchantName) ? "RECEBEDOR" : _merchantName);

        // 60: Merchant City (máx 15)
        AppendEmvTag(sb, "60", string.IsNullOrWhiteSpace(_merchantCity) ? "BRASILIA" : _merchantCity);

        // 62: Additional Data Field Template (TxId)
        var additionalData = new StringBuilder();
        AppendEmvTag(additionalData, "05", _txId);
        AppendEmvTag(sb, "62", additionalData.ToString());

        // 63: CRC16 (Tag 63 + tamanho "04" seguido do hash)
        sb.Append("6304");
        var crc = Crc16Ccitt.ComputeHex(sb.ToString());
        sb.Append(crc);

        return sb.ToString();
    }

    private static void AppendEmvTag(StringBuilder sb, string tag, string value)
    {
        var length = Encoding.UTF8.GetByteCount(value).ToString("D2", CultureInfo.InvariantCulture);
        sb.Append(tag).Append(length).Append(value);
    }

    private static string Sanitize(string input, int maxLength)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        // Remove acentos
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var result = sb.ToString().ToUpperInvariant().Trim();
        return result.Length > maxLength ? result[..maxLength] : result;
    }
}

