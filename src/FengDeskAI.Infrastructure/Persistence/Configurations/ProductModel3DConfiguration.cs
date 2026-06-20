using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductModel3DConfiguration : IEntityTypeConfiguration<ProductModel3D>
{
    public void Configure(EntityTypeBuilder<ProductModel3D> builder)
    {
        builder.ToTable("product_model3ds");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(m => m.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(m => m.SourceImageUrl).HasColumnName("source_image_url").IsRequired();
        builder.Property(m => m.MeshyTaskId).HasColumnName("meshy_task_id");
        builder.Property(m => m.ModelUrl).HasColumnName("model_url");
        builder.Property(m => m.ThumbnailUrl).HasColumnName("thumbnail_url");
        builder.Property(m => m.Progress).HasColumnName("progress").HasDefaultValue(0);
        builder.Property(m => m.ErrorMessage).HasColumnName("error_message");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // 1–1: mỗi product có tối đa một model 3D.
        builder.HasIndex(m => m.ProductId).IsUnique();
        builder.HasOne(m => m.Product)
            .WithOne(p => p.Model3D)
            .HasForeignKey<ProductModel3D>(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
