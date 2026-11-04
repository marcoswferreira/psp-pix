using Domain.Entities;
using Domain.Interfaces.Repositories;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Database.Repositories;

public class ChargeRepository(ApplicationDbContext db) : IChargeRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task AddAsync(PixCharge charge, CancellationToken ct = default)
    {
        _db.Charges.Add(charge);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PixCharge?> GetByTxIdAsync(string txId, CancellationToken ct = default)
    {
        return await _db.Charges.FirstOrDefaultAsync(c => c.TxId == txId, ct);
    }

    public async Task<PixCharge?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Charges.FindAsync(new object[] { id }, ct);
    }

    public async Task UpdateAsync(PixCharge charge, CancellationToken ct = default)
    {
        _db.Charges.Update(charge);
        await _db.SaveChangesAsync(ct);
    }
}

