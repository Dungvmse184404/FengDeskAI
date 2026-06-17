using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ChatboxConfiguration : IEntityTypeConfiguration<Chatbox>
{
    public void Configure(EntityTypeBuilder<Chatbox> builder)
    {
        builder.ToTable("chatboxes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");

        builder.Property(c => c.Type).HasColumnName("type").HasConversion<int>().IsRequired();
        builder.Property(c => c.SenderUserId).HasColumnName("sender_user_id").IsRequired();
        builder.Property(c => c.RecipientUserId).HasColumnName("recipient_user_id"); // nullable → hội thoại với AI
        builder.Property(c => c.ProductId).HasColumnName("product_id");

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Direct: cặp (sender, recipient) đã chuẩn hoá ở repo nên unique chặn trùng A→B / B→A.
        // Assistant: recipient_user_id = NULL → Postgres coi mỗi NULL là khác nhau nên không bị chặn
        // (một user có thể có nhiều hội thoại AI, vd theo từng sản phẩm).
        builder.HasIndex(c => new { c.SenderUserId, c.RecipientUserId }).IsUnique();
        builder.HasIndex(c => c.SenderUserId);
        builder.HasIndex(c => c.RecipientUserId);
        builder.HasIndex(c => c.ProductId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.SenderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sản phẩm bị xoá → chỉ gỡ ngữ cảnh, KHÔNG xoá hội thoại.
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chatbox)
            .HasForeignKey(m => m.ChatboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
