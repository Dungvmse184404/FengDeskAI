using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("user_address");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.WardId).HasColumnName("ward_id").IsRequired();

        builder.Property(a => a.StreetAddress).HasColumnName("street_address").HasMaxLength(255).IsRequired();
        builder.Property(a => a.RecipientName).HasColumnName("recipient_name").HasMaxLength(255).IsRequired();
        builder.Property(a => a.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(20).IsRequired();
        builder.Property(a => a.Latitude).HasColumnName("latitude").HasPrecision(10, 8);
        builder.Property(a => a.Longitude).HasColumnName("longitude").HasPrecision(11, 8);
        builder.Property(a => a.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
        builder.Property(a => a.Label).HasColumnName("label").HasMaxLength(50);

        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.CreatedBy).HasColumnName("created_by");
        builder.Property(a => a.UpdatedBy).HasColumnName("updated_by");
        builder.Property(a => a.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(a => a.UserId);

        // Mỗi user chỉ 1 địa chỉ mặc định — partial unique index (giống workspace default)
        builder.HasIndex(a => a.UserId)
            .HasDatabaseName("UX_user_address_user_default")
            .IsUnique()
            .HasFilter("is_default = TRUE AND is_deleted = FALSE");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Ward)
            .WithMany()
            .HasForeignKey(a => a.WardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(a => !a.IsDeleted);
    }
}
