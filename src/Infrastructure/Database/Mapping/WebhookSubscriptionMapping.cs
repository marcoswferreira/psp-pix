using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Mapping;

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

