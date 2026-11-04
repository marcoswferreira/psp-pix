using Microsoft.EntityFrameworkCore;
using MiniPspPix.Domain.Charges;
using MiniPspPix.Domain.Idempotency;
using MiniPspPix.Domain.Ledger;
using MiniPspPix.Domain.Outbox;
using MiniPspPix.Domain.Webhooks;

namespace MiniPspPix.Infrastructure.Persistence;

public class PixDbContext : DbContext
{
    public PixDbContext(DbContextOptions<PixDbContext> options) : base(options) { }

    public DbSet<PixCharge> Charges => Set<PixCharge>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PixCharge
        modelBuilder.Entity<PixCharge>(b =>
        {
            b.HasKey(c => c.Id);
            b.HasIndex(c => c.TxId).IsUnique();
            b.Property(c => c.TxId).HasMaxLength(35).IsRequired();
            b.Property(c => c.Amount).HasPrecision(18, 2).IsRequired();
            b.Property(c => c.PixKeyValue).HasMaxLength(100).IsRequired();
            b.Property(c => c.Description).HasMaxLength(140);
            b.Property(c => c.MerchantName).HasMaxLength(25).IsRequired();
            b.Property(c => c.MerchantCity).HasMaxLength(15).IsRequired();
            b.Property(c => c.BrCode).IsRequired();
            b.Property(c => c.EndToEndId).HasMaxLength(32);
        });

        // LedgerAccount
        modelBuilder.Entity<LedgerAccount>(b =>
        {
            b.HasKey(a => a.Id);
            b.HasIndex(a => a.AccountNumber).IsUnique();
            b.Property(a => a.AccountNumber).HasMaxLength(50).IsRequired();
            b.Property(a => a.Balance).HasPrecision(18, 2).IsRequired();
            b.Property(a => a.Currency).HasMaxLength(3).IsRequired();

            // Token de Concorrência Otimista: garante que dois updates simultâneos colidam
            b.Property(a => a.Version).IsConcurrencyToken();
        });

        // LedgerTransaction
        modelBuilder.Entity<LedgerTransaction>(b =>
        {
            b.HasKey(t => t.Id);
            b.HasIndex(t => new { t.ReferenceType, t.ReferenceId });
            b.Property(t => t.ReferenceType).HasMaxLength(50).IsRequired();
            b.Property(t => t.ReferenceId).HasMaxLength(100).IsRequired();

            b.HasMany(t => t.Entries)
                .WithOne()
                .HasForeignKey(e => e.TransactionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // LedgerEntry
        modelBuilder.Entity<LedgerEntry>(b =>
        {
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.AccountId);
            b.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            b.Property(e => e.Description).HasMaxLength(200).IsRequired();
        });

        // IdempotencyRecord
        modelBuilder.Entity<IdempotencyRecord>(b =>
        {
            b.HasKey(r => r.Key);
            b.Property(r => r.Key).HasMaxLength(100);
            b.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
            b.Property(r => r.Route).HasMaxLength(100).IsRequired();
        });

        // OutboxMessage
        modelBuilder.Entity<OutboxMessage>(b =>
        {
            b.HasKey(m => m.Id);
            b.HasIndex(m => m.ProcessedAtUtc);
            b.Property(m => m.EventType).HasMaxLength(100).IsRequired();
            b.Property(m => m.PayloadJson).IsRequired();
        });

        // WebhookSubscription
        modelBuilder.Entity<WebhookSubscription>(b =>
        {
            b.HasKey(s => s.Id);
            b.HasIndex(s => s.MerchantId);
            b.Property(s => s.MerchantId).HasMaxLength(50).IsRequired();
            b.Property(s => s.TargetUrl).HasMaxLength(500).IsRequired();
            b.Property(s => s.SecretKey).HasMaxLength(100).IsRequired();
        });

        // WebhookDelivery
        modelBuilder.Entity<WebhookDelivery>(b =>
        {
            b.HasKey(d => d.Id);
            b.HasIndex(d => new { d.Status, d.NextRetryAtUtc });
            b.Property(d => d.TargetUrl).HasMaxLength(500).IsRequired();
            b.Property(d => d.EventType).HasMaxLength(100).IsRequired();
            b.Property(d => d.Signature).HasMaxLength(150).IsRequired();
        });
    }
}

