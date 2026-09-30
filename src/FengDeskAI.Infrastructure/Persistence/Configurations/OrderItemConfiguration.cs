using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(i => i.ProductItemId).HasColumnName("product_item_id");
        // Null khi đơn online chưa thanh toán — delivery chỉ tạo sau khi đã trả tiền (COD tạo ngay).
        builder.Property(i => i.DeliveryId).HasColumnName("delivery_id");

        builder.Property(i => i.ProductName).HasColumnName("product_name").HasMaxLength(255).IsRequired();
        builder.Property(i => i.UnitPrice).HasColumnName("unit_price").HasPrecision(12, 2);
        builder.Property(i => i.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(i => i.ProductId).HasColumnName("product_id");
        builder.Property(i => i.GardenStoreId).HasColumnName("garden_store_id");
        builder.Property(i => i.VariantName).HasColumnName("variant_name").HasMaxLength(255);
        builder.Property(i => i.Sku).HasColumnName("sku").HasMaxLength(20);
        builder.Property(i => i.ImageUrl).HasColumnName("image_url").HasMaxLength(1000);
        // Thống kê cửa hàng lọc món theo vườn.
        builder.HasIndex(i => i.GardenStoreId);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne(i => i.Delivery)
            .WithMany(d => d.Items)
            .HasForeignKey(i => i.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.ProductItem)
            .WithMany()
            .HasForeignKey(i => i.ProductItemId)
            // Manager xoá cứng sản phẩm ⇒ món trong đơn cũ còn nguyên (đọc từ cột chụp), chỉ mất liên kết.
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
