using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.ChatboxId).HasColumnName("chatbox_id").IsRequired();
        builder.Property(m => m.SenderUserId).HasColumnName("sender_user_id"); // nullable → tin nhắn của AI
        builder.Property(m => m.SenderRole).HasColumnName("sender_role").HasConversion<int>().IsRequired();
        builder.Property(m => m.SenderName).HasColumnName("sender_name").HasMaxLength(100);
        builder.Property(m => m.Content).HasColumnName("content").HasMaxLength(5000); // nullable → tin nhắn chỉ có ảnh
        builder.Property(m => m.IsFromAi).HasColumnName("is_from_ai").HasDefaultValue(false);
        builder.Property(m => m.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        builder.Property(m => m.ReadAt).HasColumnName("read_at");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(m => new { m.ChatboxId, m.CreatedAt });
        builder.HasIndex(m => new { m.ChatboxId, m.IsRead });
        builder.HasIndex(m => m.SenderUserId);

        // Người gửi bị xoá → giữ lại lịch sử, chỉ gỡ liên kết (sender = null).
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.SenderUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(m => m.Images)
            .WithOne(i => i.Message)
            .HasForeignKey(i => i.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
