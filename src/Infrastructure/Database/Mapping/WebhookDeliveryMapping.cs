using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mapping;

public class WebhookDeliveryMapping : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.Status);
        builder.HasIndex(d => d.NextRetryAtUtc);
        builder.Property(d => d.TargetUrl).HasMaxLength(500).IsRequired();
        builder.Property(d => d.EventType).HasMaxLength(100).IsRequired();
        builder.Property(d => d.PayloadJson).IsRequired();
        builder.Property(d => d.Signature).HasMaxLength(256).IsRequired();
        builder.Property(d => d.LastError).HasMaxLength(1000);
    }
}

