using FengDeskAI.Domain.Entities.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ShippingWebhookConfiguration : IEntityTypeConfiguration<ShippingWebhook>
{
    public void Configure(EntityTypeBuilder<ShippingWebhook> builder)
    {
        builder.ToTable("shipping_webhook");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.Provider).HasColumnName("provider").HasMaxLength(50);
        builder.Property(w => w.EventType).HasColumnName("event_type").HasMaxLength(100);
        builder.Property(w => w.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(w => w.IsProcessed).HasColumnName("is_processed").HasDefaultValue(false);
        builder.Property(w => w.ReceivedAt).HasColumnName("received_at");

        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(w => w.IsProcessed);
        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
