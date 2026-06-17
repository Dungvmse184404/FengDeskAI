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
        builder.Property(m => m.SenderId).HasColumnName("sender_id"); // null = AI/system
        builder.Property(m => m.SenderType).HasColumnName("sender_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.SenderName).HasColumnName("sender_name").HasMaxLength(100);
        builder.Property(m => m.Content).HasColumnName("content").HasMaxLength(5000); // null nếu chỉ ảnh

        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by");
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by");
        builder.Property(m => m.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(m => new { m.ChatboxId, m.CreatedAt });
        builder.HasIndex(m => m.SenderId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(m => m.Images)
            .WithOne(i => i.Message)
            .HasForeignKey(i => i.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
