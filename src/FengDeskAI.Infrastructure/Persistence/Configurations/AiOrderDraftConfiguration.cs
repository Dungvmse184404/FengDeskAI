using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums.CustomerCare;
using FengDeskAI.Domain.Enums.Payment;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class AiOrderDraftConfiguration : IEntityTypeConfiguration<AiOrderDraft>
{
    public void Configure(EntityTypeBuilder<AiOrderDraft> builder)
    {
        builder.ToTable("ai_order_drafts");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(d => d.ChatboxId).HasColumnName("chatbox_id").IsRequired();
        builder.Property(d => d.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(d => d.ProductItemId).HasColumnName("product_item_id").IsRequired();
        builder.Property(d => d.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(d => d.ShippingAddressId).HasColumnName("shipping_address_id").IsRequired();
        builder.Property(d => d.PaymentMethod).HasColumnName("payment_method").HasConversion<string>().HasMaxLength(30)
            .HasDefaultValue(PaymentMethod.PayOS);

        // Cùng precision với product_items.price — lệch kiểu thì so sánh đổi giá sai ở phần thập phân.
        builder.Property(d => d.UnitPriceSnapshot).HasColumnName("unit_price_snapshot").HasPrecision(12, 2).IsRequired();
        builder.Property(d => d.ShippingFeeSnapshot).HasColumnName("shipping_fee_snapshot").HasPrecision(12, 2).IsRequired();
        builder.Property(d => d.TotalAmountSnapshot).HasColumnName("total_amount_snapshot").HasPrecision(12, 2).IsRequired();
        builder.Property(d => d.ProductName).HasColumnName("product_name").HasMaxLength(255).IsRequired();
        builder.Property(d => d.VariantName).HasColumnName("variant_name").HasMaxLength(255);
        builder.Property(d => d.AddressText).HasColumnName("address_text").HasMaxLength(500).IsRequired();

        builder.Property(d => d.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(d => d.ExpiresAt).HasColumnName("expires_at").IsRequired();

        builder.Property(d => d.CreatedAt).HasColumnName("created_at");
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by");
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by");
        builder.Property(d => d.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Mỗi phòng chỉ có MỘT draft đang mở — DB tự chặn, không phó mặc code.
        builder.HasIndex(d => new { d.UserId, d.ChatboxId })
            .IsUnique()
            .HasFilter("status = 'Pending' AND is_deleted = false")
            .HasDatabaseName("ux_ai_order_drafts_user_chatbox_pending");
        // Worker dọn draft hết hạn / kẹt Confirming.
        builder.HasIndex(d => new { d.Status, d.ExpiresAt })
            .HasDatabaseName("ix_ai_order_drafts_status_expires");

        // Draft là dữ liệu tạm: bên kia bị xóa cứng thì draft đi theo.
        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Chatbox>().WithMany().HasForeignKey(d => d.ChatboxId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>().WithMany().HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ProductItem>().WithMany().HasForeignKey(d => d.ProductItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<UserAddress>().WithMany().HasForeignKey(d => d.ShippingAddressId).OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(d => !d.IsDeleted);
    }
}
