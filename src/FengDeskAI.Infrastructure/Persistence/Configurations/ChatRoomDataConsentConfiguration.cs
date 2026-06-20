using FengDeskAI.Domain.Entities.Chat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ChatRoomDataConsentConfiguration : IEntityTypeConfiguration<ChatRoomDataConsent>
{
    public void Configure(EntityTypeBuilder<ChatRoomDataConsent> builder)
    {
        builder.ToTable("chat_room_data_consents");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.ChatboxId).HasColumnName("chatbox_id").IsRequired();
        builder.Property(c => c.GranterUserId).HasColumnName("granter_user_id").IsRequired();
        builder.Property(c => c.ShareProfile).HasColumnName("share_profile").HasDefaultValue(false);
        builder.Property(c => c.ShareWorkspaces).HasColumnName("share_workspaces").HasDefaultValue(false);
        builder.Property(c => c.ShareOrders).HasColumnName("share_orders").HasDefaultValue(false);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // Mỗi (phòng, người cấp) tối đa một bản ghi consent.
        builder.HasIndex(c => new { c.ChatboxId, c.GranterUserId }).IsUnique();

        builder.HasOne(c => c.Chatbox)
            .WithMany()
            .HasForeignKey(c => c.ChatboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
