using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductStyleConfiguration : IEntityTypeConfiguration<ProductStyle>
{
    public void Configure(EntityTypeBuilder<ProductStyle> builder)
    {
        builder.ToTable("product_styles");

        // Junction thuần: composite PK (product_id, style), enum lưu string.
        builder.Property(ps => ps.Style).HasColumnName("style").HasConversion<string>().HasMaxLength(30);
        builder.HasKey(ps => new { ps.ProductId, ps.Style });
        builder.Property(ps => ps.ProductId).HasColumnName("product_id");

        // Quan hệ tới Product cấu hình ở ProductConfiguration.
    }
}
