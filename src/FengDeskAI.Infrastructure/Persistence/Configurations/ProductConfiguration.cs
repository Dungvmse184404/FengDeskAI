using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.GardenStoreId).HasColumnName("garden_store_id").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(p => p.GardenStoreId);

        builder.HasOne(p => p.Store)
            .WithMany()
            .HasForeignKey(p => p.GardenStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Items)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Images)
            .WithOne(i => i.Product)
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProductCategories)
            .WithOne(pc => pc.Product)
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.ProductTags)
            .WithOne(pt => pt.Product)
            .HasForeignKey(pt => pt.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(p => !p.IsDeleted);
    }
}
