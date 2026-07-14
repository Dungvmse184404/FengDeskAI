using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("refunds");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ReturnRequestId).HasColumnName("return_request_id").IsRequired();
        builder.Property(r => r.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(r => r.TransactionId).HasColumnName("transaction_id");

        builder.Property(r => r.Amount).HasColumnName("amount").HasPrecision(12, 2);
        builder.Property(r => r.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.ProviderRefundId).HasColumnName("provider_refund_id").HasMaxLength(100);
        builder.Property(r => r.ProcessedBy).HasColumnName("processed_by");
        builder.Property(r => r.ProcessedAt).HasColumnName("processed_at");
        builder.Property(r => r.CompletedAt).HasColumnName("completed_at");
        builder.Property(r => r.Note).HasColumnName("note");

        // RMA v2: idempotency + retry + can thiệp thủ công (audit trail).
        builder.Property(r => r.Gateway).HasColumnName("gateway").HasMaxLength(30).HasDefaultValue("payos");
        builder.Property(r => r.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(120).IsRequired();
        builder.Property(r => r.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
        builder.Property(r => r.IsManual).HasColumnName("is_manual").HasDefaultValue(false);
        builder.Property(r => r.ManualReason).HasColumnName("manual_reason");
        builder.Property(r => r.EvidenceUrl).HasColumnName("evidence_url").HasMaxLength(500);
        builder.Property(r => r.PerformedBy).HasColumnName("performed_by");

        builder.HasIndex(r => r.IdempotencyKey).IsUnique();

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(r => r.OrderId);
        builder.HasIndex(r => r.ReturnRequestId).IsUnique();

        // Quan hệ 1-1 với ReturnRequest đã cấu hình ở ReturnRequestConfiguration.
        builder.HasOne(r => r.Order)
            .WithMany()
            .HasForeignKey(r => r.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(r => r.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
