using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(d => d.GardenStoreId).HasColumnName("garden_store_id").IsRequired();

        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(50);
        builder.Property(d => d.TrackingCode).HasColumnName("tracking_code").HasMaxLength(100);
        builder.Property(d => d.ProviderOrderId).HasColumnName("provider_order_id").HasMaxLength(100);
        builder.Property(d => d.ShippingProvider).HasColumnName("shipping_provider").HasMaxLength(50);
        builder.Property(d => d.TrackingUrl).HasColumnName("tracking_url").HasMaxLength(500);
        builder.Property(d => d.ShippingFee).HasColumnName("shipping_fee").HasPrecision(12, 2);
        builder.Property(d => d.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        builder.Property(d => d.IsExchange).HasColumnName("is_exchange").HasDefaultValue(false);
        builder.Property(d => d.AssignedAt).HasColumnName("assigned_at");
        builder.Property(d => d.ShippedAt).HasColumnName("shipped_at");
        builder.Property(d => d.DeliveredAt).HasColumnName("delivered_at");
        builder.Property(d => d.EstimatedDeliveryDate).HasColumnName("estimated_delivery_date");

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(d => d.GardenStoreId);

        builder.HasOne(d => d.Store)
            .WithMany()
            .HasForeignKey(d => d.GardenStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
