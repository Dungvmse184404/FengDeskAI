using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("recommendations");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.WorkspaceProfileId).HasColumnName("workspace_profile_id").IsRequired();
        builder.Property(r => r.WorkspaceTypeId).HasColumnName("workspace_type_id");

        builder.Property(r => r.CustomerElement).HasColumnName("customer_element").HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.KuaNumber).HasColumnName("kua_number");
        builder.Property(r => r.KuaGroup).HasColumnName("kua_group").HasConversion<string>().HasMaxLength(10);
        builder.Property(r => r.PersonalWeight).HasColumnName("personal_weight").HasColumnType("numeric(4,2)");
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Summary).HasColumnName("summary").HasColumnType("text");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(r => r.UserId);
        builder.HasIndex(r => r.WorkspaceProfileId);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict để tránh nhiều đường cascade tới users (rec → user và rec → profile → user).
        builder.HasOne(r => r.WorkspaceProfile)
            .WithMany()
            .HasForeignKey(r => r.WorkspaceProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WorkspaceType>()
            .WithMany()
            .HasForeignKey(r => r.WorkspaceTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(r => r.Items)
            .WithOne(i => i.Recommendation)
            .HasForeignKey(i => i.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Logs)
            .WithOne(l => l.Recommendation)
            .HasForeignKey(l => l.RecommendationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
