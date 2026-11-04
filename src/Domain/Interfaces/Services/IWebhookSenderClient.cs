namespace Domain.Interfaces.Services;

public interface IWebhookSenderClient
{
    Task<(int StatusCode, string? Error)> SendAsync(string url, string payloadJson, string signature, CancellationToken ct = default);
}

