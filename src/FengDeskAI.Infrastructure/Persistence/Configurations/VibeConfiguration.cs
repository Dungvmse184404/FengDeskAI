using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class VibeConfiguration : IEntityTypeConfiguration<Vibe>
{
    public void Configure(EntityTypeBuilder<Vibe> builder)
    {
        builder.ToTable("vibes");

        builder.HasKey(v => v.Code);
        builder.Property(v => v.Code).HasColumnName("code").HasMaxLength(20);
        builder.Property(v => v.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(v => v.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(v => v.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);
    }
}
