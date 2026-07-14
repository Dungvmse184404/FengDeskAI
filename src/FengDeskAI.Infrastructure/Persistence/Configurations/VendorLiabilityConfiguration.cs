using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Vendor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class VendorLiabilityConfiguration : IEntityTypeConfiguration<VendorLiability>
{
    public void Configure(EntityTypeBuilder<VendorLiability> builder)
    {
        builder.ToTable("vendor_liabilities");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.GardenStoreId).HasColumnName("garden_id").IsRequired();
        builder.Property(v => v.ReturnRequestId).HasColumnName("ticket_id").IsRequired();
        builder.Property(v => v.RefundId).HasColumnName("refund_id");

        builder.Property(v => v.Amount).HasColumnName("amount").HasPrecision(12, 2);
        builder.Property(v => v.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.DisputeReason).HasColumnName("dispute_reason");
        builder.Property(v => v.DisputeDeadline).HasColumnName("dispute_deadline").IsRequired();
        builder.Property(v => v.ResolvedBy).HasColumnName("resolved_by");
        builder.Property(v => v.ResolvedAt).HasColumnName("resolved_at");

        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");
        builder.Property(v => v.CreatedBy).HasColumnName("created_by");
        builder.Property(v => v.UpdatedBy).HasColumnName("updated_by");
        builder.Property(v => v.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(v => v.GardenStoreId);
        builder.HasIndex(v => v.Status);

        builder.HasOne(v => v.ReturnRequest)
            .WithOne(r => r.VendorLiability)
            .HasForeignKey<VendorLiability>(v => v.ReturnRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.Refund)
            .WithMany()
            .HasForeignKey(v => v.RefundId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.Garden)
            .WithMany()
            .HasForeignKey(v => v.GardenStoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(v => !v.IsDeleted);
    }
}
