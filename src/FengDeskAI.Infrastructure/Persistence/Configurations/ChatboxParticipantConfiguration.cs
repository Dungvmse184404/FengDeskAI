using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ChatboxParticipantConfiguration : IEntityTypeConfiguration<ChatboxParticipant>
{
    public void Configure(EntityTypeBuilder<ChatboxParticipant> builder)
    {
        builder.ToTable("chatbox_participants");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.ChatboxId).HasColumnName("chatbox_id").IsRequired();
        builder.Property(p => p.UserId).HasColumnName("user_id"); // null = AI
        builder.Property(p => p.ParticipantType).HasColumnName("participant_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Role).HasColumnName("role").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.IsMuted).HasColumnName("is_muted").HasDefaultValue(false);
        builder.Property(p => p.IsHidden).HasColumnName("is_hidden").HasDefaultValue(false);
        builder.Property(p => p.LastReadAt).HasColumnName("last_read_at");
        builder.Property(p => p.JoinedAt).HasColumnName("joined_at");

        // Mỗi user chỉ 1 dòng/phòng (AI user_id null → Postgres không chặn, nhưng ta chỉ thêm 1 AiBot/phòng ở tầng app).
        builder.HasIndex(p => new { p.ChatboxId, p.UserId }).IsUnique();
        builder.HasIndex(p => p.UserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
