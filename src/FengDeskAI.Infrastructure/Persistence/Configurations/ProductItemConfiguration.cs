using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductItemConfiguration : IEntityTypeConfiguration<ProductItem>
{
    public void Configure(EntityTypeBuilder<ProductItem> builder)
    {
        builder.ToTable("product_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(i => i.Name).HasColumnName("name").HasMaxLength(100);
        builder.Property(i => i.Price).HasColumnName("price").HasPrecision(12, 2).IsRequired();
        builder.Property(i => i.Stock).HasColumnName("stock").HasDefaultValue(0).IsRequired();
        builder.Property(i => i.Sku).HasColumnName("sku").HasMaxLength(20);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(i => i.ProductId);
        builder.HasIndex(i => i.Sku).IsUnique().HasFilter("sku IS NOT NULL AND is_deleted = FALSE");

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
