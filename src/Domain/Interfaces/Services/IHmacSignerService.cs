namespace Domain.Interfaces.Services;

public interface IHmacSignerService
{
    string CreateSignature(string payloadJson, string secretKey, long? timestamp = null);
    bool VerifySignature(string payloadJson, string headerValue, string secretKey, TimeSpan? tolerance = null);
}

