using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ElementInputMapConfiguration : IEntityTypeConfiguration<ElementInputMap>
{
    public void Configure(EntityTypeBuilder<ElementInputMap> builder)
    {
        builder.ToTable("element_input_map");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.InputKind).HasColumnName("input_kind").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.InputCode).HasColumnName("input_code").HasMaxLength(30).IsRequired();
        builder.Property(e => e.Element).HasColumnName("element").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Weight).HasColumnName("weight").HasColumnType("numeric(4,3)").HasDefaultValue(1.0m);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(e => new { e.InputKind, e.InputCode, e.Element })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
