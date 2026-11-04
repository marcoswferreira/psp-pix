using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Infrastructure.Idempotency;

public static class IdempotencyHasher
{
    public static string ComputeRequestHash(string method, string route, string? body)
    {
        var canonicalBody = CanonicalizeJson(body);
        var rawContent = $"{method.ToUpperInvariant()}:{route.ToLowerInvariant()}:{canonicalBody}";
        
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawContent));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string CanonicalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null) return string.Empty;

            var sortedNode = SortJsonNode(node);
            return sortedNode?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? string.Empty;
        }
        catch
        {
            // If it's not valid JSON, trim whitespace
            return json.Trim();
        }
    }

    private static JsonNode? SortJsonNode(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var sortedObj = new JsonObject();
            var sortedProperties = obj.OrderBy(kvp => kvp.Key, StringComparer.Ordinal).ToList();
            foreach (var prop in sortedProperties)
            {
                sortedObj.Add(prop.Key, SortJsonNode(prop.Value?.DeepClone()));
            }
            return sortedObj;
        }

        if (node is JsonArray arr)
        {
            var sortedArr = new JsonArray();
            foreach (var item in arr)
            {
                sortedArr.Add(SortJsonNode(item?.DeepClone()));
            }
            return sortedArr;
        }

        return node?.DeepClone();
    }
}

