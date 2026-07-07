using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkspaceProfileInputConfiguration : IEntityTypeConfiguration<WorkspaceProfileInput>
{
    public void Configure(EntityTypeBuilder<WorkspaceProfileInput> builder)
    {
        builder.ToTable("workspace_profile_inputs");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.WorkspaceProfileId).HasColumnName("workspace_profile_id").IsRequired();
        builder.Property(e => e.InputKind).HasColumnName("input_kind").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.InputCode).HasColumnName("input_code").HasMaxLength(30).IsRequired();

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Workspace.WorkspaceProfile>()
            .WithMany()
            .HasForeignKey(e => e.WorkspaceProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.WorkspaceProfileId);
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
