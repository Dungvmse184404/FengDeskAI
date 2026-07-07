using FengDeskAI.Domain.Entities.Recommendation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkspaceTypeElementConfiguration : IEntityTypeConfiguration<WorkspaceTypeElement>
{
    public void Configure(EntityTypeBuilder<WorkspaceTypeElement> builder)
    {
        builder.ToTable("workspace_type_elements");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.WorkspaceTypeId).HasColumnName("workspace_type_id").IsRequired();
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(10).IsRequired();
        builder.Property(e => e.Element).HasColumnName("element").HasConversion<string>().HasMaxLength(10);
        builder.Property(e => e.Weight).HasColumnName("weight").HasColumnType("numeric(4,3)");

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasOne<Domain.Entities.Workspace.WorkspaceType>()
            .WithMany()
            .HasForeignKey(e => e.WorkspaceTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.WorkspaceTypeId, e.Source, e.Element })
            .IsUnique()
            .HasFilter("is_deleted = false");

        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
