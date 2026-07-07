using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkPurposeElementModifierConfiguration : IEntityTypeConfiguration<WorkPurposeElementModifier>
{
    public void Configure(EntityTypeBuilder<WorkPurposeElementModifier> builder)
    {
        builder.ToTable("work_purpose_element_modifiers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.WorkPurpose).HasColumnName("work_purpose").HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Element).HasColumnName("element").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Delta).HasColumnName("delta").HasColumnType("numeric(4,3)");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(e => new { e.WorkPurpose, e.Element })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
