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

