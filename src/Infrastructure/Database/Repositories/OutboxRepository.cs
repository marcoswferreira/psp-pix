using Domain.Entities;
using Domain.Interfaces.Repositories;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class OutboxRepository(ApplicationDbContext db) : IOutboxRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(message);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 20, CancellationToken ct = default)
    {
        return await _db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (msg != null)
        {
            msg.MarkAsProcessed();
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RecordFailureAsync(Guid messageId, string error, CancellationToken ct = default)
    {
        var msg = await _db.OutboxMessages.FindAsync(new object[] { messageId }, ct);
        if (msg != null)
        {
            msg.RecordFailure(error);
            await _db.SaveChangesAsync(ct);
        }
    }
}

