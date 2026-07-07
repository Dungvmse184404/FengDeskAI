using FengDeskAI.Domain.Entities.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkspaceTypeConfiguration : IEntityTypeConfiguration<WorkspaceType>
{
    public void Configure(EntityTypeBuilder<WorkspaceType> builder)
    {
        builder.ToTable("workspace_types");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("id");

        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(t => t.IsPublic).HasColumnName("is_public").HasDefaultValue(false);
        builder.Property(t => t.PersonalWeight).HasColumnName("personal_weight").HasColumnType("numeric(4,2)").HasDefaultValue(1.0m);
        builder.Property(t => t.Scope).HasColumnName("scope").HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Enums.Workspace.WorkspaceScope.Private);
        builder.Property(t => t.IsSystemSeeded).HasColumnName("is_system_seeded").HasDefaultValue(false);

        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");
        builder.Property(t => t.CreatedBy).HasColumnName("created_by");
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by");
        builder.Property(t => t.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(t => t.Name);

        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
