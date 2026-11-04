using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mapping;

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

