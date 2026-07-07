using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ScoringParamConfiguration : IEntityTypeConfiguration<ScoringParam>
{
    public void Configure(EntityTypeBuilder<ScoringParam> builder)
    {
        builder.ToTable("scoring_params");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(30).IsRequired();
        builder.Property(e => e.Value).HasColumnName("value").HasColumnType("numeric(5,3)");
        builder.Property(e => e.Description).HasColumnName("description").HasMaxLength(200);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(e => e.Code).IsUnique().HasFilter("is_deleted = false");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
