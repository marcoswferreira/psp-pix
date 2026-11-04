using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mapping;

public class PixChargeMapping : IEntityTypeConfiguration<PixCharge>
{
    public void Configure(EntityTypeBuilder<PixCharge> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasIndex(c => c.TxId).IsUnique();
        builder.Property(c => c.TxId).HasMaxLength(35).IsRequired();
        builder.Property(c => c.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(c => c.PixKeyValue).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Description).HasMaxLength(140);
        builder.Property(c => c.MerchantName).HasMaxLength(25).IsRequired();
        builder.Property(c => c.MerchantCity).HasMaxLength(15).IsRequired();
        builder.Property(c => c.BrCode).IsRequired();
        builder.Property(c => c.EndToEndId).HasMaxLength(32);
    }
}

public class LedgerAccountMapping : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.AccountNumber).IsUnique();
        builder.Property(a => a.AccountNumber).HasMaxLength(50).IsRequired();
        builder.Property(a => a.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.Currency).HasMaxLength(3).IsRequired();
        builder.Property(a => a.Version).IsConcurrencyToken();
    }
}

public class LedgerTransactionMapping : IEntityTypeConfiguration<LedgerTransaction>
{
    public void Configure(EntityTypeBuilder<LedgerTransaction> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => new { t.ReferenceType, t.ReferenceId });
        builder.Property(t => t.ReferenceType).HasMaxLength(50).IsRequired();
        builder.Property(t => t.ReferenceId).HasMaxLength(100).IsRequired();

        builder.HasMany(t => t.Entries)
            .WithOne()
            .HasForeignKey(e => e.TransactionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LedgerEntryMapping : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => e.AccountId);
        builder.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(200).IsRequired();
    }
}

public class IdempotencyRecordMapping : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(r => r.Key);
        builder.Property(r => r.Key).HasMaxLength(100);
        builder.Property(r => r.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(r => r.Route).HasMaxLength(100).IsRequired();
    }
}

public class OutboxMessageMapping : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(m => m.Id);
        builder.HasIndex(m => m.ProcessedAtUtc);
        builder.Property(m => m.EventType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.PayloadJson).IsRequired();
    }
}

public class WebhookSubscriptionMapping : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.MerchantId);
        builder.Property(s => s.MerchantId).HasMaxLength(50).IsRequired();
        builder.Property(s => s.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(s => s.SecretKey).HasMaxLength(100).IsRequired();
    }
}

public class WebhookDeliveryMapping : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => new { d.Status, d.NextRetryAtUtc });
        builder.Property(d => d.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(d => d.EventType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Signature).HasMaxLength(150).IsRequired();
    }
}

