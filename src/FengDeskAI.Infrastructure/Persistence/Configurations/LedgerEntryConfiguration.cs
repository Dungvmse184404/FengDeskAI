using FengDeskAI.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries", t =>
            // Sổ nhà vườn phải biết là vườn nào; sổ sàn thì không thuộc vườn nào.
            t.HasCheckConstraint("ck_ledger_entries_garden_account",
                "(account = 'GardenStore' AND garden_store_id IS NOT NULL) OR (account = 'Platform' AND garden_store_id IS NULL)"));

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Account).HasColumnName("account").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(e => e.GardenStoreId).HasColumnName("garden_store_id");
        builder.Property(e => e.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasPrecision(14, 2).IsRequired();
        builder.Property(e => e.AvailableAt).HasColumnName("available_at").IsRequired();
        builder.Property(e => e.OrderId).HasColumnName("order_id");
        builder.Property(e => e.DeliveryId).HasColumnName("delivery_id");
        builder.Property(e => e.RefundId).HasColumnName("refund_id");
        builder.Property(e => e.VendorLiabilityId).HasColumnName("vendor_liability_id");
        builder.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        builder.Property(e => e.Note).HasColumnName("note").HasMaxLength(500);

        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by");
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by");
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Chốt chặn cuối cùng chống ghi trùng — dịch vụ đã kiểm trước, index này bắt nốt trường hợp chạy song song.
        builder.HasIndex(e => e.IdempotencyKey).IsUnique().HasDatabaseName("ux_ledger_entries_idempotency_key");
        // Số dư / thống kê của một vườn luôn lọc theo (account, vườn, mốc khả dụng).
        builder.HasIndex(e => new { e.Account, e.GardenStoreId, e.AvailableAt })
            .HasDatabaseName("ix_ledger_entries_balance");
        builder.HasIndex(e => e.DeliveryId);
        builder.HasIndex(e => e.VendorLiabilityId);

        builder.HasOne<Domain.Entities.Vendor.GardenStore>()
            .WithMany()
            .HasForeignKey(e => e.GardenStoreId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Entities.Sales.Delivery>()
            .WithMany()
            .HasForeignKey(e => e.DeliveryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Refund>()
            .WithMany()
            .HasForeignKey(e => e.RefundId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<VendorLiability>()
            .WithMany()
            .HasForeignKey(e => e.VendorLiabilityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Bút toán không bao giờ bị xoá mềm, nhưng giữ bộ lọc cho đồng bộ với mọi bảng khác.
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
