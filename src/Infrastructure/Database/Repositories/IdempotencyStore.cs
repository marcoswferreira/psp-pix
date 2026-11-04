using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class IdempotencyStore(ApplicationDbContext db) : IIdempotencyStore
{
    private readonly ApplicationDbContext _db = db;

    public async Task<IdempotencyRecord?> GetAsync(string key, CancellationToken ct = default)
    {
        return await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);
    }

    public async Task<bool> TryAcquireLockAsync(string key, string requestHash, string route, TimeSpan lockDuration, CancellationToken ct = default)
    {
        var existing = await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);

        if (existing == null)
        {
            var record = new IdempotencyRecord(key, requestHash, route, lockDuration);
            _db.IdempotencyRecords.Add(record);
            try
            {
                await _db.SaveChangesAsync(ct);
                return true;
            }
            catch (DbUpdateException)
            {
                return false;
            }
        }

        if (existing.Status == IdempotencyStatus.InProgress && existing.HasLockExpired())
        {
            _db.IdempotencyRecords.Remove(existing);
            await _db.SaveChangesAsync(ct);

            var newRecord = new IdempotencyRecord(key, requestHash, route, lockDuration);
            _db.IdempotencyRecords.Add(newRecord);
            await _db.SaveChangesAsync(ct);
            return true;
        }

        return false;
    }

    public async Task SaveResultAsync(string key, int statusCode, string responseBody, CancellationToken ct = default)
    {
        var record = await _db.IdempotencyRecords.FindAsync(new object[] { key }, ct);
        if (record != null)
        {
            record.Complete(statusCode, responseBody);
            await _db.SaveChangesAsync(ct);
        }
    }
}

