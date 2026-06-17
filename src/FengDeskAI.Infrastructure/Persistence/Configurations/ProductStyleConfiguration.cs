using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductStyleConfiguration : IEntityTypeConfiguration<ProductStyle>
{
    public void Configure(EntityTypeBuilder<ProductStyle> builder)
    {
        builder.ToTable("product_styles");

        // Junction thuần: composite PK (product_id, style_code).
        builder.Property(ps => ps.StyleCode).HasColumnName("style_code").HasMaxLength(30);
        builder.HasKey(ps => new { ps.ProductId, ps.StyleCode });
        builder.Property(ps => ps.ProductId).HasColumnName("product_id");

        // FK tới bảng tra cứu styles (Restrict: không xoá style đang được dùng).
        builder.HasOne(ps => ps.Style)
            .WithMany()
            .HasForeignKey(ps => ps.StyleCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(ps => ps.StyleCode);

        // Quan hệ tới Product cấu hình ở ProductConfiguration.
    }
}
