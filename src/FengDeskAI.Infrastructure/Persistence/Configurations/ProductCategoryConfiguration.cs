using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories");

        // Junction thuần: composite PK, không audit/soft-delete.
        builder.HasKey(pc => new { pc.ProductId, pc.CategoryId });
        builder.Property(pc => pc.ProductId).HasColumnName("product_id");
        builder.Property(pc => pc.CategoryId).HasColumnName("category_id");

        builder.HasIndex(pc => pc.CategoryId);

        builder.HasOne(pc => pc.Category)
            .WithMany()
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);
        // Quan hệ tới Product cấu hình ở ProductConfiguration.
    }
}
