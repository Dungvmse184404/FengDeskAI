using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class GardenStoreConfiguration : IEntityTypeConfiguration<GardenStore>
{
    public void Configure(EntityTypeBuilder<GardenStore> builder)
    {
        builder.ToTable("garden_stores");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.OwnerUserId).HasColumnName("owner_id").IsRequired();

        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description");
        builder.Property(s => s.Hotline).HasColumnName("hotline").HasMaxLength(20).IsRequired();
        builder.Property(s => s.OpeningHours).HasColumnName("opening_hours").HasMaxLength(100);
        builder.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.CreatedBy).HasColumnName("created_by");
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by");
        builder.Property(s => s.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(s => s.OwnerUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(s => s.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Address)
            .WithOne(a => a.Store)
            .HasForeignKey<StoreAddress>(a => a.StoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.StaffAssignments)
            .WithOne(a => a.Store)
            .HasForeignKey(a => a.GardenStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
