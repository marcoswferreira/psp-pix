using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IChargeRepository
{
    Task AddAsync(PixCharge charge, CancellationToken ct = default);
    Task<PixCharge?> GetByTxIdAsync(string txId, CancellationToken ct = default);
    Task<PixCharge?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(PixCharge charge, CancellationToken ct = default);
}

