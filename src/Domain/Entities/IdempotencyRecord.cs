using Domain.Enums;

namespace Domain.Entities;

public class IdempotencyRecord
{
    public string Key { get; private set; }
    public string RequestHash { get; private set; }
    public string Route { get; private set; }
    public IdempotencyStatus Status { get; private set; }
    public int? StatusCode { get; private set; }
    public string? ResponseBody { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LockedUntilUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    protected IdempotencyRecord()
    {
        Key = null!;
        RequestHash = null!;
        Route = null!;
    }

    public IdempotencyRecord(string key, string requestHash, string route, TimeSpan? lockDuration = null)
    {
        Key = key;
        RequestHash = requestHash;
        Route = route;
        Status = IdempotencyStatus.InProgress;
        CreatedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = CreatedAtUtc.Add(lockDuration ?? TimeSpan.FromSeconds(30));
    }

    public void Complete(int statusCode, string responseBody)
    {
        Status = IdempotencyStatus.Completed;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CompletedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = null;
    }

    public void Fail(int statusCode, string responseBody)
    {
        Status = IdempotencyStatus.Failed;
        StatusCode = statusCode;
        ResponseBody = responseBody;
        CompletedAtUtc = DateTime.UtcNow;
        LockedUntilUtc = null;
    }

    public bool HasLockExpired() => LockedUntilUtc.HasValue && DateTime.UtcNow > LockedUntilUtc.Value;
}

