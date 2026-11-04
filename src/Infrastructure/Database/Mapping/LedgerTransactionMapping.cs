using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mapping;

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

