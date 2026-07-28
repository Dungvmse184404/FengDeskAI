using FengDeskAI.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class AuthorizationAuditLogConfiguration : IEntityTypeConfiguration<AuthorizationAuditLog>
{
    public void Configure(EntityTypeBuilder<AuthorizationAuditLog> builder)
    {
        builder.ToTable("authorization_audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceType).HasColumnName("resource_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.ResourceId).HasColumnName("resource_id");
        builder.Property(x => x.OldValueJson).HasColumnName("old_value_json").HasColumnType("jsonb");
        builder.Property(x => x.NewValueJson).HasColumnName("new_value_json").HasColumnType("jsonb");
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        builder.HasIndex(x => x.ActorUserId);
        builder.HasIndex(x => new { x.ResourceType, x.ResourceId });
        builder.HasIndex(x => x.CreatedAt);
    }
}
