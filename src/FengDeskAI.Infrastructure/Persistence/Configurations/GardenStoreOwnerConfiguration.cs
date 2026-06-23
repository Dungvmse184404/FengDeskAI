using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class GardenStoreOwnerConfiguration : IEntityTypeConfiguration<GardenStoreOwner>
{
    public void Configure(EntityTypeBuilder<GardenStoreOwner> builder)
    {
        builder.ToTable("garden_store_owners");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.GardenStoreId).HasColumnName("garden_store_id").IsRequired();
        builder.Property(o => o.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(o => o.IsPrimary).HasColumnName("is_primary").HasDefaultValue(false);
        builder.Property(o => o.AssignedAt).HasColumnName("assigned_at");

        builder.Property(o => o.CreatedAt).HasColumnName("created_at");
        builder.Property(o => o.UpdatedAt).HasColumnName("updated_at");
        builder.Property(o => o.CreatedBy).HasColumnName("created_by");
        builder.Property(o => o.UpdatedBy).HasColumnName("updated_by");
        builder.Property(o => o.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Mỗi user chỉ là owner của một store đúng một lần.
        builder.HasIndex(o => new { o.GardenStoreId, o.OwnerUserId }).IsUnique();

        builder.HasOne(o => o.Store)
            .WithMany(s => s.Owners)
            .HasForeignKey(o => o.GardenStoreId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(o => o.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(o => !o.IsDeleted);
    }
}
