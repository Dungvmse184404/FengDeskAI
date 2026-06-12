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

        builder.Property(c => c.SenderUserId).HasColumnName("sender_user_id").IsRequired();
        builder.Property(c => c.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(c => new { c.SenderUserId, c.RecipientUserId }).IsUnique();
        builder.HasIndex(c => c.SenderUserId);
        builder.HasIndex(c => c.RecipientUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.SenderUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chatbox)
            .HasForeignKey(m => m.ChatboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
