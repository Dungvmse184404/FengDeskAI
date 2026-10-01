using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class OrderStoreChargeConfiguration : IEntityTypeConfiguration<OrderStoreCharge>
{
    public void Configure(EntityTypeBuilder<OrderStoreCharge> builder)
    {
        builder.ToTable("order_store_charges");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(c => c.GardenStoreId).HasColumnName("garden_store_id").IsRequired();
        builder.Property(c => c.Subtotal).HasColumnName("subtotal").HasPrecision(12, 2);
        builder.Property(c => c.ShippingFee).HasColumnName("shipping_fee").HasPrecision(12, 2);
        builder.Property(c => c.ShippingDiscount).HasColumnName("shipping_discount").HasPrecision(12, 2);
        builder.Property(c => c.PlatformItemDiscount).HasColumnName("platform_item_discount").HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(c => c.SellerItemDiscount).HasColumnName("seller_item_discount").HasPrecision(12, 2).HasDefaultValue(0m);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Mỗi vườn đúng một dòng trong một đơn.
        builder.HasIndex(c => new { c.OrderId, c.GardenStoreId }).IsUnique();

        builder.HasOne<Domain.Entities.Vendor.GardenStore>()
            .WithMany()
            .HasForeignKey(c => c.GardenStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
