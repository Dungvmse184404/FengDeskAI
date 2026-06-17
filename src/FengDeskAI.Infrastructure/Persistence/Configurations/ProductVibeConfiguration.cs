using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductVibeConfiguration : IEntityTypeConfiguration<ProductVibe>
{
    public void Configure(EntityTypeBuilder<ProductVibe> builder)
    {
        builder.ToTable("product_vibes");

        // Junction thuần: composite PK (product_id, vibe), enum lưu string.
        builder.Property(pv => pv.Vibe).HasColumnName("vibe").HasConversion<string>().HasMaxLength(20);
        builder.HasKey(pv => new { pv.ProductId, pv.Vibe });
        builder.Property(pv => pv.ProductId).HasColumnName("product_id");

        // Quan hệ tới Product cấu hình ở ProductConfiguration.
    }
}
