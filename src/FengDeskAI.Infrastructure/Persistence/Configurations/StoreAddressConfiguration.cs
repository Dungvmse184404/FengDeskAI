using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class StoreAddressConfiguration : IEntityTypeConfiguration<StoreAddress>
{
    public void Configure(EntityTypeBuilder<StoreAddress> builder)
    {
        builder.ToTable("stores_address");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.StoreId).HasColumnName("store_id").IsRequired();
        builder.Property(a => a.WardId).HasColumnName("ward_id").IsRequired();

        builder.Property(a => a.StreetAddress).HasColumnName("street_address").HasMaxLength(255).IsRequired();
        builder.Property(a => a.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
        builder.Property(a => a.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
        builder.Property(a => a.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Relationship Store↔Address configured ở GardenStoreConfiguration (1-1).
        builder.HasOne(a => a.Ward)
            .WithMany()
            .HasForeignKey(a => a.WardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
