using System.Globalization;
using System.Text;
using Domain.Common.Exceptions;
using Domain.Interfaces.Services;

namespace Infrastructure.Services;

public class BrCodeService : IBrCodeService
{
    private const ushort CrcPolynomial = 0x1021;
    private const ushort CrcInitialValue = 0xFFFF;

    public string ComputeCrc16Hex(string payload)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        ushort crc = CrcInitialValue;

        foreach (var b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ CrcPolynomial);
                else
                    crc = (ushort)(crc << 1);
            }
        }

        return crc.ToString("X4", CultureInfo.InvariantCulture);
    }

    public string GenerateBrCode(string pixKey, decimal amount, string merchantName, string merchantCity, string txId, string? description = null, bool isDynamic = false)
    {
        if (string.IsNullOrWhiteSpace(pixKey))
            throw new DomainException("Chave Pix é obrigatória para o BR Code.");

        if (amount < 0)
            throw new DomainException("Valor do Pix não pode ser negativo.");

        var sb = new StringBuilder();

        AppendEmvTag(sb, "00", "01");
        AppendEmvTag(sb, "01", isDynamic ? "12" : "11");

        var merchantAccountInfo = new StringBuilder();
        AppendEmvTag(merchantAccountInfo, "00", "br.gov.bcb.pix");
        AppendEmvTag(merchantAccountInfo, "01", pixKey.Trim());
        if (!string.IsNullOrEmpty(description))
            AppendEmvTag(merchantAccountInfo, "02", Sanitize(description, 50));
        AppendEmvTag(sb, "26", merchantAccountInfo.ToString());

        AppendEmvTag(sb, "52", "0000");
        AppendEmvTag(sb, "53", "986");

        if (amount > 0)
            AppendEmvTag(sb, "54", amount.ToString("F2", CultureInfo.InvariantCulture));

        AppendEmvTag(sb, "58", "BR");
        AppendEmvTag(sb, "59", Sanitize(merchantName, 25));
        AppendEmvTag(sb, "60", Sanitize(merchantCity, 15));

        var additionalData = new StringBuilder();
        AppendEmvTag(additionalData, "05", string.IsNullOrWhiteSpace(txId) ? "***" : txId.Trim());
        AppendEmvTag(sb, "62", additionalData.ToString());

        sb.Append("6304");
        var crc = ComputeCrc16Hex(sb.ToString());
        sb.Append(crc);

        return sb.ToString();
    }

    public string GenerateSvg(string payload, int size = 280)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {size} {size}\" width=\"{size}\" height=\"{size}\">");
        sb.Append("<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\" rx=\"16\" stroke=\"#32BCAD\" stroke-width=\"4\"/>");
        sb.Append("<g fill=\"#32BCAD\"><circle cx=\"140\" cy=\"45\" r=\"24\" fill=\"#E8F8F5\"/>");
        sb.Append("<text x=\"140\" y=\"52\" font-family=\"sans-serif\" font-size=\"14\" font-weight=\"bold\" text-anchor=\"middle\" fill=\"#32BCAD\">PIX</text></g>");
        sb.Append("<g transform=\"translate(40, 80)\"><rect width=\"200\" height=\"130\" fill=\"#F8F9FA\" rx=\"8\" stroke=\"#E5E7EB\"/>");
        DrawCorner(sb, 10, 10);
        DrawCorner(sb, 150, 10);
        DrawCorner(sb, 10, 80);

        var hash = (uint)payload.GetHashCode();
        for (var row = 0; row < 6; row++)
        {
            for (var col = 0; col < 10; col++)
            {
                var bit = (hash >> ((row * 10 + col) % 32)) & 1;
                if (bit == 1 && !IsInsideCorner(row, col))
                {
                    sb.Append($"<rect x=\"{60 + col * 8}\" y=\"{15 + row * 16}\" width=\"6\" height=\"6\" fill=\"#111827\" rx=\"1\"/>");
                }
            }
        }
        sb.Append("</g>");
        sb.Append("<text x=\"140\" y=\"235\" font-family=\"sans-serif\" font-size=\"11\" font-weight=\"bold\" text-anchor=\"middle\" fill=\"#1F2937\">Pague com Pix Copia e Cola</text>");
        sb.Append("<text x=\"140\" y=\"255\" font-family=\"monospace\" font-size=\"8\" text-anchor=\"middle\" fill=\"#6B7280\">Bacen EMV QRCPS-MPM</text>");
        sb.Append("</svg>");
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

    private static void DrawCorner(StringBuilder sb, int x, int y)
    {
        sb.Append($"<rect x=\"{x}\" y=\"{y}\" width=\"40\" height=\"40\" fill=\"none\" stroke=\"#111827\" stroke-width=\"4\" rx=\"4\"/>");
        sb.Append($"<rect x=\"{x + 10}\" y=\"{y + 10}\" width=\"20\" height=\"20\" fill=\"#111827\" rx=\"2\"/>");
    }

    private static bool IsInsideCorner(int row, int col)
    {
        if (row <= 2 && col <= 2) return true;
        if (row <= 2 && col >= 7) return true;
        if (row >= 4 && col <= 2) return true;
        return false;
    }
}

