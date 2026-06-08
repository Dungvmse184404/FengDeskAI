using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class GardenStaffAssignmentConfiguration : IEntityTypeConfiguration<GardenStaffAssignment>
{
    public void Configure(EntityTypeBuilder<GardenStaffAssignment> builder)
    {
        builder.ToTable("garden_staff_assignments");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.GardenStoreId).HasColumnName("garden_store_id").IsRequired();
        builder.Property(a => a.StaffId).HasColumnName("staff_id").IsRequired();
        builder.Property(a => a.AssignedBy).HasColumnName("assigned_by").IsRequired();

        builder.Property(a => a.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(a => a.AssignedAt).HasColumnName("assigned_at");
        builder.Property(a => a.UnassignedAt).HasColumnName("unassigned_at");

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Một staff chỉ có 1 phân công đang active cho mỗi store
        builder.HasIndex(a => new { a.GardenStoreId, a.StaffId })
            .HasDatabaseName("UX_garden_staff_active")
            .IsUnique()
            .HasFilter("is_active = TRUE AND is_deleted = FALSE");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.StaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
