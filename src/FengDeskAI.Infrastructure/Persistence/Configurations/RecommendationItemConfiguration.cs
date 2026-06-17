using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.CustomerCare;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class RecommendationItemConfiguration : IEntityTypeConfiguration<RecommendationItem>
{
    public void Configure(EntityTypeBuilder<RecommendationItem> builder)
    {
        builder.ToTable("recommendation_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.RecommendationId).HasColumnName("recommendation_id").IsRequired();
        builder.Property(i => i.ProductId).HasColumnName("product_id").IsRequired();

        builder.Property(i => i.BaseScore).HasColumnName("base_score").HasColumnType("numeric(6,3)");
        builder.Property(i => i.BaseRank).HasColumnName("base_rank");
        builder.Property(i => i.FinalRank).HasColumnName("final_rank");

        builder.Property(i => i.MatchFacts).HasColumnName("match_facts").HasColumnType("jsonb").IsRequired();
        builder.Property(i => i.CautionFacts).HasColumnName("caution_facts").HasColumnType("jsonb");
        builder.Property(i => i.AiExplanation).HasColumnName("ai_explanation").HasColumnType("text");

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(i => i.RecommendationId);
        builder.HasIndex(i => i.ProductId);

        builder.HasOne(i => i.Product)
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        // Quan hệ tới Recommendation cấu hình ở RecommendationConfiguration.

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
