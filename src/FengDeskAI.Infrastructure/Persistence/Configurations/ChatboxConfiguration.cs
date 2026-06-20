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

        builder.Property(c => c.IsGroup).HasColumnName("is_group").HasDefaultValue(true);
        builder.Property(c => c.Title).HasColumnName("title").HasMaxLength(200);
        builder.Property(c => c.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(c => c.ProductId).HasColumnName("product_id");
        builder.Property(c => c.IsAiEnabled).HasColumnName("is_ai_enabled").HasDefaultValue(false);
        builder.Property(c => c.IsSupport).HasColumnName("is_support").HasDefaultValue(false);

        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.CreatedBy).HasColumnName("created_by");
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by");
        builder.Property(c => c.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(c => c.ProductId);
        builder.HasIndex(c => c.CreatedByUserId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.CreatedByUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(c => c.ProductId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(c => c.Participants)
            .WithOne(p => p.Chatbox)
            .HasForeignKey(p => p.ChatboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Chatbox)
            .HasForeignKey(m => m.ChatboxId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
