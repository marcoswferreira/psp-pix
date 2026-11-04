using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default);
    Task<bool> TryAcquireLockAsync(string key, string requestHash, string route, TimeSpan lockDuration, CancellationToken ct = default);
    Task SaveResultAsync(string key, int statusCode, string responseBody, CancellationToken ct = default);
}

