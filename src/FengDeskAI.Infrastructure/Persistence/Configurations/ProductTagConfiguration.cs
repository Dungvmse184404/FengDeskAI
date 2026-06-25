using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductTagConfiguration : IEntityTypeConfiguration<ProductTag>
{
    public void Configure(EntityTypeBuilder<ProductTag> builder)
    {
        builder.ToTable("product_tags");

        // Junction thuần: composite PK, không audit/soft-delete.
        builder.HasKey(pt => new { pt.ProductId, pt.TagId });
        builder.Property(pt => pt.ProductId).HasColumnName("product_id");
        builder.Property(pt => pt.TagId).HasColumnName("tag_id");

        builder.HasIndex(pt => pt.TagId);

        builder.HasOne(pt => pt.Tag)
            .WithMany()
            .HasForeignKey(pt => pt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        // Tags đã ngừng dùng ở luồng product (bảng ma): Product không còn nav ProductTags,
        // quan hệ tới Product khai báo phía junction để giữ nguyên FK/bảng product_tags.
        builder.HasOne(pt => pt.Product)
            .WithMany()
            .HasForeignKey(pt => pt.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
