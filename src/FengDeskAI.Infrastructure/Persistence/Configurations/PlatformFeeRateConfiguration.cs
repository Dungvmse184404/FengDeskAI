using FengDeskAI.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class PlatformFeeRateConfiguration : IEntityTypeConfiguration<PlatformFeeRate>
{
    public void Configure(EntityTypeBuilder<PlatformFeeRate> builder)
    {
        builder.ToTable("platform_fee_rates", t =>
            t.HasCheckConstraint("ck_platform_fee_rates_range", "commission_rate >= 0 AND commission_rate <= 0.3"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.CommissionRate).HasColumnName("commission_rate").HasPrecision(5, 4).IsRequired();
        builder.Property(e => e.EffectiveFrom).HasColumnName("effective_from").IsRequired();
        builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(e => e.EffectiveFrom).HasDatabaseName("ix_platform_fee_rates_effective_from");
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
