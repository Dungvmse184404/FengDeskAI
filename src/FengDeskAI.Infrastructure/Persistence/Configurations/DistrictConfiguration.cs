using FengDeskAI.Domain.Entities.Geography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> builder)
    {
        builder.ToTable("districts");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.ProvinceId).HasColumnName("province_id").IsRequired();

        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
        builder.Property(d => d.Code).HasColumnName("code").IsRequired();
        builder.Property(d => d.GhnDistrictId).HasColumnName("ghn_district_id");
        builder.HasIndex(d => d.Code).IsUnique();
        builder.HasIndex(d => d.ProvinceId);

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasMany(d => d.Wards)
            .WithOne(w => w.District)
            .HasForeignKey(w => w.DistrictId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
