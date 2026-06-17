using FengDeskAI.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ChatMessageImageConfiguration : IEntityTypeConfiguration<ChatMessageImage>
{
    public void Configure(EntityTypeBuilder<ChatMessageImage> builder)
    {
        builder.ToTable("chat_message_images");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");

        builder.Property(i => i.ChatMessageId).HasColumnName("chat_message_id").IsRequired();
        builder.Property(i => i.Url).HasColumnName("url").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(i => i.ChatMessageId);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
