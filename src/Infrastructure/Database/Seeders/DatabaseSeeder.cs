using Domain.Entities;
using Domain.Enums;
using Infrastructure.Database.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Database.Seeders;

public static class DatabaseSeeder
{
    public static readonly Guid DefaultPixTransitAccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultMerchantAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static async Task SeedAsync(ApplicationDbContext db, ILogger logger)
    {
        if (!await db.LedgerAccounts.AnyAsync())
        {
            logger.LogInformation("Seeding initial ledger accounts (Transit and Merchant)...");

            db.LedgerAccounts.AddRange(
                new LedgerAccount(DefaultPixTransitAccountId, "TRANSIT-BACEN-001", AccountType.PixSettlementTransit, 0m),
                new LedgerAccount(DefaultMerchantAccountId, "MERCHANT-001", AccountType.Merchant, 1000.00m)
            );

            await db.SaveChangesAsync();
            logger.LogInformation("Initial ledger accounts seeded successfully.");
        }
    }
}

