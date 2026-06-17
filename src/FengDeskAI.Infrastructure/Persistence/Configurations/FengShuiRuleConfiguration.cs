using FengDeskAI.Domain.Entities.CustomerCare;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class FengShuiRuleConfiguration : IEntityTypeConfiguration<FengShuiRule>
{
    public void Configure(EntityTypeBuilder<FengShuiRule> builder)
    {
        builder.ToTable("feng_shui_rules");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.SubjectElement).HasColumnName("subject_element").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(r => r.ObjectElement).HasColumnName("object_element").HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(r => r.Relation).HasColumnName("relation").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Score).HasColumnName("score").HasColumnType("numeric(4,2)");
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(500);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Mỗi cặp (mệnh người, hành sản phẩm) chỉ có 1 luật.
        builder.HasIndex(r => new { r.SubjectElement, r.ObjectElement })
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
