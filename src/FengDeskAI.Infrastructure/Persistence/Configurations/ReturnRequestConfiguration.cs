using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("return_requests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(r => r.DeliveryId).HasColumnName("delivery_id").IsRequired();
        builder.Property(r => r.CustomerId).HasColumnName("customer_id").IsRequired();

        builder.Property(r => r.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Reason).HasColumnName("reason").HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.ReasonDetail).HasColumnName("reason_detail");

        builder.Property(r => r.RefundAmount).HasColumnName("refund_amount").HasPrecision(12, 2);
        builder.Property(r => r.RefundMethod).HasColumnName("refund_method").HasConversion<string>().HasMaxLength(20);

        builder.Property(r => r.BankAccountName).HasColumnName("bank_account_name").HasMaxLength(100);
        builder.Property(r => r.BankAccountNumber).HasColumnName("bank_account_number").HasMaxLength(50);
        builder.Property(r => r.BankName).HasColumnName("bank_name").HasMaxLength(100);

        builder.Property(r => r.ReturnTrackingCode).HasColumnName("return_tracking_code").HasMaxLength(100);
        builder.Property(r => r.ApprovedBy).HasColumnName("approved_by");
        builder.Property(r => r.ApprovedAt).HasColumnName("approved_at");
        builder.Property(r => r.RejectedReason).HasColumnName("rejected_reason");
        builder.Property(r => r.ReceivedAt).HasColumnName("received_at");
        builder.Property(r => r.ReplacementDeliveryId).HasColumnName("replacement_delivery_id");

        // RMA v2: SLA vendor + bổ sung bằng chứng + người quyết định (luôn Staff).
        builder.Property(r => r.VendorResponse).HasColumnName("vendor_response").HasConversion<string>().HasMaxLength(20).HasDefaultValue(Domain.Enums.Sales.VendorResponse.Pending);
        builder.Property(r => r.VendorResponseDeadline).HasColumnName("vendor_response_deadline");
        builder.Property(r => r.EvidenceDeadline).HasColumnName("evidence_deadline");
        builder.Property(r => r.DecidedBy).HasColumnName("decided_by");
        builder.Property(r => r.DecidedAt).HasColumnName("decided_at");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.DeliveryId);
        builder.HasIndex(r => r.CustomerId);

        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Delivery)
            .WithMany()
            .HasForeignKey(r => r.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Items)
            .WithOne(i => i.ReturnRequest)
            .HasForeignKey(i => i.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Images)
            .WithOne(i => i.ReturnRequest)
            .HasForeignKey(i => i.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.StatusLogs)
            .WithOne(l => l.ReturnRequest)
            .HasForeignKey(l => l.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Refund)
            .WithOne(f => f.ReturnRequest)
            .HasForeignKey<Domain.Entities.Payment.Refund>(f => f.ReturnRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
