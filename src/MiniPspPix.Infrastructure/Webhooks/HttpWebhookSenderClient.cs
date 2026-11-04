using System.Net.Http.Headers;
using System.Text;
using MiniPspPix.Application.Webhooks;

namespace MiniPspPix.Infrastructure.Webhooks;

public class HttpWebhookSenderClient : IWebhookSenderClient
{
    private readonly HttpClient _httpClient;

    public HttpWebhookSenderClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(int StatusCode, string? Error)> SendAsync(
        string url,
        string payloadJson,
        string signature,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Signature", signature);
        request.Headers.Add("User-Agent", "MiniPspPix-WebhookDelivery/1.0");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                return ((int)response.StatusCode, null);
            }

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

