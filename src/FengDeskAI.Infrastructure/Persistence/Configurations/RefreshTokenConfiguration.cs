using FengDeskAI.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);
        builder.Property(rt => rt.Id).HasColumnName("id");
        builder.Property(rt => rt.UserId).HasColumnName("user_id").IsRequired();

        builder.Property(rt => rt.Token).HasColumnName("token").IsRequired();
        builder.HasIndex(rt => rt.Token).IsUnique();

        builder.Property(rt => rt.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(rt => rt.IsRevoked).HasColumnName("is_revoked").HasDefaultValue(false);
        builder.Property(rt => rt.RevokedAt).HasColumnName("revoked_at");
        builder.Property(rt => rt.ReplacedByToken).HasColumnName("replaced_by_token");

        builder.Property(rt => rt.CreatedAt).HasColumnName("created_at");
        builder.Property(rt => rt.UpdatedAt).HasColumnName("updated_at");
        builder.Property(rt => rt.CreatedBy).HasColumnName("created_by");
        builder.Property(rt => rt.UpdatedBy).HasColumnName("updated_by");
        builder.Property(rt => rt.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.Ignore(rt => rt.IsActive);

        builder.HasIndex(rt => new { rt.UserId, rt.IsRevoked });

        builder.HasQueryFilter(rt => !rt.IsDeleted);
    }
}
