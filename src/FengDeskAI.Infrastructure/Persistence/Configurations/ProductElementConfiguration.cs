using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductElementConfiguration : IEntityTypeConfiguration<ProductElement>
{
    public void Configure(EntityTypeBuilder<ProductElement> builder)
    {
        builder.ToTable("product_element");

        // Composite key (product_id, element) — junction nhiều-nhiều.
        builder.HasKey(e => new { e.ProductId, e.Element });

        builder.Property(e => e.ProductId).HasColumnName("product_id");
        builder.Property(e => e.Element).HasColumnName("element").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false);

        // Index lọc nhanh hành chính của sản phẩm.
        builder.HasIndex(e => new { e.ProductId, e.IsPrimary });

        // Quan hệ Product → Elements (cascade) cấu hình ở ProductConfiguration.
    }
}
