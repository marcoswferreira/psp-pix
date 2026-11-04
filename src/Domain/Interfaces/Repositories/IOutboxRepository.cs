using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize = 20, CancellationToken ct = default);
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken ct = default);
    Task RecordFailureAsync(Guid messageId, string error, CancellationToken ct = default);
}

