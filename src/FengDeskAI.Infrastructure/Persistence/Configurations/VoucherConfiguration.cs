using FengDeskAI.Domain.Entities.Promotion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("vouchers", t =>
        {
            // Chốt ở DB: câu trừ lượt khi hủy đơn không bao giờ đẩy số lượt xuống âm.
            t.HasCheckConstraint("ck_vouchers_used_count", "used_count >= 0");
            t.HasCheckConstraint("ck_vouchers_min_subtotal", "min_order_subtotal >= 0");
        });

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(v => v.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(v => v.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(v => v.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(30);
        builder.Property(v => v.FundedBy).HasColumnName("funded_by").HasConversion<string>().HasMaxLength(30);
        builder.Property(v => v.MinOrderSubtotal).HasColumnName("min_order_subtotal").HasPrecision(12, 2);
        builder.Property(v => v.MaxDiscountAmount).HasColumnName("max_discount_amount").HasPrecision(12, 2);
        builder.Property(v => v.ProvinceId).HasColumnName("province_id");
        builder.Property(v => v.StartsAt).HasColumnName("starts_at");
        builder.Property(v => v.EndsAt).HasColumnName("ends_at");
        builder.Property(v => v.UsageLimit).HasColumnName("usage_limit");
        builder.Property(v => v.UsageLimitPerUser).HasColumnName("usage_limit_per_user");
        builder.Property(v => v.UsedCount).HasColumnName("used_count").HasDefaultValue(0);
        builder.Property(v => v.IsAutoApply).HasColumnName("is_auto_apply").HasDefaultValue(false);
        builder.Property(v => v.IsActive).HasColumnName("is_active").HasDefaultValue(true);

        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(v => v.Code).IsUnique();

        builder.HasOne<Domain.Entities.Geography.Province>()
            .WithMany()
            .HasForeignKey(v => v.ProvinceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
