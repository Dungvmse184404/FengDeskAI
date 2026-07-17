using FengDeskAI.Domain.Entities.Workspace;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class WorkspaceProductPlacementConfiguration : IEntityTypeConfiguration<WorkspaceProductPlacement>
{
    public void Configure(EntityTypeBuilder<WorkspaceProductPlacement> builder)
    {
        builder.ToTable("workspace_product_placements");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(p => p.WorkspaceProfileId).HasColumnName("workspace_profile_id").IsRequired();
        builder.Property(p => p.OrderItemId).HasColumnName("order_item_id").IsRequired();
        builder.Property(p => p.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(p => p.PlacedAt).HasColumnName("placed_at");

        // Cột base — repo này KHÔNG dùng naming convention tự động, phải map tay như mọi config khác.
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.CreatedBy).HasColumnName("created_by");
        builder.Property(p => p.UpdatedBy).HasColumnName("updated_by");
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasQueryFilter(p => !p.IsDeleted);

        // 1 món đã mua chỉ nằm ở 1 phòng. Filter is_deleted để gỡ ra đặt lại không đụng unique.
        builder.HasIndex(p => p.OrderItemId).IsUnique().HasFilter("is_deleted = FALSE");
        builder.HasIndex(p => new { p.WorkspaceProfileId, p.UserId });

        builder.HasOne(p => p.WorkspaceProfile)
            .WithMany()
            .HasForeignKey(p => p.WorkspaceProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.OrderItem)
            .WithMany()
            .HasForeignKey(p => p.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Product)
            .WithMany()
            .HasForeignKey(p => p.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
