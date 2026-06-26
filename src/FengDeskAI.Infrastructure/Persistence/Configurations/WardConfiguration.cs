using FengDeskAI.Domain.Entities.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WardConfiguration : IEntityTypeConfiguration<Ward>
{
    public void Configure(EntityTypeBuilder<Ward> builder)
    {
        builder.ToTable("wards");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasColumnName("id");
        builder.Property(w => w.DistrictId).HasColumnName("district_id").IsRequired();

        builder.Property(w => w.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(w => w.Code).HasColumnName("code").IsRequired();
        builder.Property(w => w.GhnWardCode).HasColumnName("ghn_ward_code").HasMaxLength(20);
        builder.HasIndex(w => w.Code).IsUnique();
        builder.HasIndex(w => w.DistrictId);

        builder.Property(w => w.CreatedAt).HasColumnName("created_at");
        builder.Property(w => w.UpdatedAt).HasColumnName("updated_at");
        builder.Property(w => w.CreatedBy).HasColumnName("created_by");
        builder.Property(w => w.UpdatedBy).HasColumnName("updated_by");
        builder.Property(w => w.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasQueryFilter(w => !w.IsDeleted);
    }
}
