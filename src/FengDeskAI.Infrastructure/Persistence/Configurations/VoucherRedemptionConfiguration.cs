using FengDeskAI.Domain.Entities.Promotion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class VoucherRedemptionConfiguration : IEntityTypeConfiguration<VoucherRedemption>
{
    public void Configure(EntityTypeBuilder<VoucherRedemption> builder)
    {
        builder.ToTable("voucher_redemptions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.VoucherId).HasColumnName("voucher_id").IsRequired();
        builder.Property(r => r.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();
        builder.Property(r => r.DiscountAmount).HasColumnName("discount_amount").HasPrecision(12, 2);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Một đơn chỉ dùng một voucher.
        builder.HasIndex(r => r.OrderId).IsUnique();
        // Đếm lượt của một người cho một voucher.
        builder.HasIndex(r => new { r.VoucherId, r.CustomerId, r.Status });

        builder.HasOne(r => r.Voucher)
            .WithMany()
            .HasForeignKey(r => r.VoucherId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Sales.Order>()
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
