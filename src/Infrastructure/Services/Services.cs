using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Domain.Common.Exceptions;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.ValueObjects;

namespace Infrastructure.Services;

public class SimulatedBankingProvider : IBankingProvider
{
    public async Task<BankChargeResult> RegisterChargeAsync(BankChargeRequest request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(request.Chaos, ct);

        if (request.Chaos?.Action == ChaosAction.Fail)
        {
            return new BankChargeResult(
                Success: false,
                BankTransactionId: null,
                ErrorCode: request.Chaos.BankErrorCode ?? "CH01",
                ErrorMessage: request.Chaos.FailureReason ?? "Rejeição bancária simulada na emissão da cobrança."
            );
        }

        return new BankChargeResult(
            Success: true,
            BankTransactionId: $"BANK-TX-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            ErrorCode: null,
            ErrorMessage: null
        );
    }

    public async Task<BankPayoutResult> SendPayoutAsync(BankPayoutRequest request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(request.Chaos, ct);

        if (request.Chaos?.Action == ChaosAction.Fail)
        {
            return new BankPayoutResult(
                Success: false,
                EndToEndId: null,
                BankTransactionId: null,
                ErrorCode: request.Chaos.BankErrorCode ?? "AB03",
                ErrorMessage: request.Chaos.FailureReason ?? "Chave Pix de destino inexistente ou conta encerrada.",
                SettledAtUtc: DateTime.UtcNow
            );
        }

        var endToEndId = EndToEndId.Generate("12345678").Value;
        var bankTxId = $"PAYOUT-SPI-{Guid.NewGuid():N}"[..24].ToUpperInvariant();

        return new BankPayoutResult(
            Success: true,
            EndToEndId: endToEndId,
            BankTransactionId: bankTxId,
            ErrorCode: null,
            ErrorMessage: null,
            SettledAtUtc: DateTime.UtcNow
        );
    }

    public async Task<SimulatedInboundPaymentResult> SimulateInboundPaymentAsync(SimulatedInboundPaymentRequest request, CancellationToken ct = default)
    {
        await ApplyChaosAsync(request.Chaos, ct);

        if (request.Chaos?.Action == ChaosAction.Fail)
        {
            return new SimulatedInboundPaymentResult(
                Success: false,
                EndToEndId: string.Empty,
                BankTransactionId: null,
                WasDuplicated: false,
                ErrorMessage: request.Chaos.FailureReason ?? "Falha simulada na liquidação."
            );
        }

        var endToEndId = EndToEndId.Generate("98765432").Value;
        var wasDuplicated = request.Chaos?.Action == ChaosAction.Duplicate;

        return new SimulatedInboundPaymentResult(
            Success: true,
            EndToEndId: endToEndId,
            BankTransactionId: $"INBOUND-{Guid.NewGuid():N}"[..20].ToUpperInvariant(),
            WasDuplicated: wasDuplicated,
            ErrorMessage: null
        );
    }

    private static async Task ApplyChaosAsync(ChaosOptions? chaos, CancellationToken ct)
    {
        if (chaos == null) return;

        if (chaos.Action == ChaosAction.Delay && chaos.DelayMilliseconds > 0)
        {
            await Task.Delay(chaos.DelayMilliseconds, ct);
        }
        else if (chaos.Action == ChaosAction.Timeout)
        {
            if (chaos.DelayMilliseconds > 0)
            {
                await Task.Delay(chaos.DelayMilliseconds, ct);
            }
            throw new TimeoutException("Timeout bancário simulado: a câmara SPI não respondeu dentro do SLA de 1000ms.");
        }
    }
}

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

public class HmacSignerService : IHmacSignerService
{
    public string CreateSignature(string payloadJson, string secretKey, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataToSign = $"{ts}.{payloadJson}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        var hexHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"t={ts},v1={hexHash}";
    }

    public bool VerifySignature(string payloadJson, string headerValue, string secretKey, TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(headerValue)) return false;

        var parts = headerValue.Split(',');
        string? timestampStr = null;
        string? signatureHex = null;

        foreach (var part in parts)
        {
            var kv = part.Trim().Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "t") timestampStr = kv[1];
            if (kv[0] == "v1") signatureHex = kv[1];
        }

        if (string.IsNullOrEmpty(timestampStr) || string.IsNullOrEmpty(signatureHex))
            return false;

        if (!long.TryParse(timestampStr, out var timestamp))
            return false;

        var maxTolerance = tolerance ?? TimeSpan.FromMinutes(5);
        var messageTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (DateTimeOffset.UtcNow - messageTime > maxTolerance || messageTime - DateTimeOffset.UtcNow > maxTolerance)
            return false;

        var expectedSignature = CreateSignature(payloadJson, secretKey, timestamp);
        var expectedParts = expectedSignature.Split(',');
        var expectedHex = expectedParts.FirstOrDefault(p => p.StartsWith("v1="))?["v1=".Length..];

        if (expectedHex == null) return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureHex),
            Encoding.UTF8.GetBytes(expectedHex)
        );
    }
}

public class HttpWebhookSenderClient(HttpClient httpClient) : IWebhookSenderClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<(int StatusCode, string? Error)> SendAsync(string url, string payloadJson, string signature, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("User-Agent", "MiniPspPix-WebhookDelivery/1.0");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
                return ((int)response.StatusCode, null);

            var content = await response.Content.ReadAsStringAsync(ct);
            return ((int)response.StatusCode, $"Falha HTTP {(int)response.StatusCode}: {content}");
        }
        catch (HttpRequestException ex)
        {
            return (0, $"HttpRequestException: {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return (408, $"Timeout na entrega do webhook: {ex.Message}");
        }
    }
}

