using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ProductElementInputConfiguration : IEntityTypeConfiguration<ProductElementInput>
{
    public void Configure(EntityTypeBuilder<ProductElementInput> builder)
    {
        builder.ToTable("product_element_inputs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(e => e.InputKind).HasColumnName("input_kind").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.InputCode).HasColumnName("input_code").HasMaxLength(30).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Catalog.Product>()
            .WithMany()
            .HasForeignKey(e => e.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.ProductId);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
