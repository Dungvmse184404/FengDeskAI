using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("cart_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.CartId).HasColumnName("cart_id").IsRequired();
        builder.Property(i => i.ProductItemId).HasColumnName("product_item_id").IsRequired();
        builder.Property(i => i.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(i => i.AddedAt).HasColumnName("added_at");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Một product item chỉ xuất hiện 1 dòng trong giỏ
        builder.HasIndex(i => new { i.CartId, i.ProductItemId })
            .HasDatabaseName("UX_cart_items_cart_product_item")
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.HasOne(i => i.ProductItem)
            .WithMany()
            .HasForeignKey(i => i.ProductItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
