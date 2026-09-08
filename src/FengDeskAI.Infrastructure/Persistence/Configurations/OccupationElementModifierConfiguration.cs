using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class OccupationElementModifierConfiguration : IEntityTypeConfiguration<OccupationElementModifier>
{
    public void Configure(EntityTypeBuilder<OccupationElementModifier> builder)
    {
        builder.ToTable("occupation_element_modifiers");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.OccupationId).HasColumnName("occupation_id");
        builder.Property(e => e.Element).HasColumnName("element").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Delta).HasColumnName("delta").HasColumnType("numeric(4,3)");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne(e => e.Occupation)
            .WithMany(o => o.Modifiers)
            .HasForeignKey(e => e.OccupationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.OccupationId, e.Element })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
