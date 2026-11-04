using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiniPspPix.Application.Idempotency;

public static class IdempotencyHasher
{
    public static string ComputeFingerprint(string method, string path, string? requestBody)
    {
        var normalizedBody = string.Empty;
        if (!string.IsNullOrWhiteSpace(requestBody))
        {
            try
            {
                var node = JsonNode.Parse(requestBody);
                normalizedBody = CanonicalizeJson(node);
            }
            catch
            {
                normalizedBody = requestBody.Trim();
            }
        }

        var raw = $"{method.ToUpperInvariant()}:{path.ToLowerInvariant()}:{normalizedBody}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CanonicalizeJson(JsonNode? node)
    {
        if (node == null) return "null";

        if (node is JsonObject obj)
        {
            var sortedProperties = obj
                .OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"\"{p.Key}\":{CanonicalizeJson(p.Value)}");

            return $"{{{string.Join(",", sortedProperties)}}}";
        }

        if (node is JsonArray arr)
        {
            var elements = arr.Select(CanonicalizeJson);
            return $"[{string.Join(",", elements)}]";
        }

        return node.ToJsonString();
    }
}
