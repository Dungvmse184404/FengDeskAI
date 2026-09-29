using FengDeskAI.Domain.Entities.CustomerCare;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.Content)
            .HasColumnName("content")
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(r => r.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        // Null sau khi sản phẩm bị xoá cứng — đánh giá vẫn giữ.
        builder.Property(r => r.ProductId).HasColumnName("product_id");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Mỗi DÒNG ĐƠN chỉ một đánh giá — mua lại sản phẩm thì được đánh giá lần nữa.
        builder.Property(r => r.OrderItemId).HasColumnName("order_item_id");
        builder.HasIndex(r => r.OrderItemId)
            .HasDatabaseName("UX_reviews_order_item")
            .IsUnique()
            .HasFilter("is_deleted = FALSE AND order_item_id IS NOT NULL");

        builder.HasIndex(r => new { r.UserId, r.ProductId })
            .HasDatabaseName("IX_reviews_user_product");

        builder.HasIndex(r => r.ProductId);
        builder.HasIndex(r => r.GardenStoreId);
        builder.Property(r => r.ProductName).HasColumnName("product_name").HasMaxLength(255);
        builder.Property(r => r.GardenStoreId).HasColumnName("garden_store_id");

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Product)
            .WithMany()
            .HasForeignKey(r => r.ProductId)
            // Xoá cứng sản phẩm KHÔNG xoá đánh giá của khách (chốt 29/09/2026).
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.OrderItem)
            .WithMany()
            .HasForeignKey(r => r.OrderItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
