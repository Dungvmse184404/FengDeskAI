using FengDeskAI.Domain.Entities.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkspaceProfileImageConfiguration : IEntityTypeConfiguration<WorkspaceProfileImage>
{
    public void Configure(EntityTypeBuilder<WorkspaceProfileImage> builder)
    {
        builder.ToTable("workspace_profile_images");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.WorkspaceProfileId).HasColumnName("workspace_profile_id").IsRequired();
        builder.Property(i => i.Url).HasColumnName("url").HasMaxLength(1000).IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne(i => i.WorkspaceProfile)
            .WithMany(w => w.Images)
            .HasForeignKey(i => i.WorkspaceProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.WorkspaceProfileId);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
