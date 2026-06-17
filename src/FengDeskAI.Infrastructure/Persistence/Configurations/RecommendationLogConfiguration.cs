using FengDeskAI.Domain.Entities.CustomerCare;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class RecommendationLogConfiguration : IEntityTypeConfiguration<RecommendationLog>
{
    public void Configure(EntityTypeBuilder<RecommendationLog> builder)
    {
        builder.ToTable("recommendation_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");

        builder.Property(l => l.RecommendationId).HasColumnName("recommendation_id").IsRequired();
        builder.Property(l => l.Stage).HasColumnName("stage").HasMaxLength(50).IsRequired();
        builder.Property(l => l.Detail).HasColumnName("detail").HasColumnType("jsonb");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(l => l.RecommendationId);

        // Quan hệ tới Recommendation cấu hình ở RecommendationConfiguration.

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
