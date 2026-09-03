using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductAspirationConfiguration : IEntityTypeConfiguration<ProductAspiration>
{
    public void Configure(EntityTypeBuilder<ProductAspiration> builder)
    {
        builder.ToTable("product_aspirations");

        // Junction thuần: composite PK (product_id, aspiration).
        builder.Property(pa => pa.ProductId).HasColumnName("product_id");
        builder.Property(pa => pa.Aspiration)
            .HasColumnName("aspiration")
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.HasKey(pa => new { pa.ProductId, pa.Aspiration });

        builder.Property(pa => pa.IsApproved).HasColumnName("is_approved").HasDefaultValue(false);
        builder.Property(pa => pa.ApprovedBy).HasColumnName("approved_by");
        builder.Property(pa => pa.ApprovedAt).HasColumnName("approved_at");

        // Engine chỉ đọc dòng đã duyệt → index lọc, không phủ toàn bảng.
        builder.HasIndex(pa => pa.Aspiration)
            .HasFilter("is_approved");

        // Quan hệ tới Product cấu hình ở ProductConfiguration.
    }
}
