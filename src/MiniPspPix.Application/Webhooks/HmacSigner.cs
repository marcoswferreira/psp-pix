using System.Security.Cryptography;
using System.Text;

namespace MiniPspPix.Application.Webhooks;

public static class HmacSigner
{
    public static string CreateSignature(string payloadJson, string secretKey, long? timestamp = null)
    {
        var ts = timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var dataToSign = $"{ts}.{payloadJson}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToSign));
        var hexHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        return $"t={ts},v1={hexHash}";
    }

    public static bool VerifySignature(
        string payloadJson,
        string headerValue,
        string secretKey,
        TimeSpan? tolerance = null)
    {
        if (string.IsNullOrWhiteSpace(headerValue))
            return false;

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

        // Proteção contra Replay Attack
        var maxTolerance = tolerance ?? TimeSpan.FromMinutes(5);
        var messageTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (DateTimeOffset.UtcNow - messageTime > maxTolerance || messageTime - DateTimeOffset.UtcNow > maxTolerance)
            return false;

        var expectedSignature = CreateSignature(payloadJson, secretKey, timestamp);
        var expectedParts = expectedSignature.Split(',');
        var expectedHex = expectedParts.FirstOrDefault(p => p.StartsWith("v1="))?["v1=".Length..];

        if (expectedHex == null)
            return false;

        // Comparação em tempo constante para evitar timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(signatureHex),
            Encoding.UTF8.GetBytes(expectedHex)
        );
    }
}

