using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mapping;

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

