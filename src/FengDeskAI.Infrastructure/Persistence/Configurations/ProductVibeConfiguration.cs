using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductVibeConfiguration : IEntityTypeConfiguration<ProductVibe>
{
    public void Configure(EntityTypeBuilder<ProductVibe> builder)
    {
        builder.ToTable("product_vibes");

        // Junction thuần: composite PK (product_id, vibe_code).
        builder.Property(pv => pv.VibeCode).HasColumnName("vibe_code").HasMaxLength(20);
        builder.HasKey(pv => new { pv.ProductId, pv.VibeCode });
        builder.Property(pv => pv.ProductId).HasColumnName("product_id");

        // FK tới bảng tra cứu vibes (Restrict: không xoá vibe đang được dùng).
        builder.HasOne(pv => pv.Vibe)
            .WithMany()
            .HasForeignKey(pv => pv.VibeCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pv => pv.VibeCode);

        // Quan hệ tới Product cấu hình ở ProductConfiguration.
    }
}
