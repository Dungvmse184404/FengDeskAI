using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class Model3DRequestConfiguration : IEntityTypeConfiguration<Model3DRequest>
{
    public void Configure(EntityTypeBuilder<Model3DRequest> builder)
    {
        builder.ToTable("model3d_requests");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(r => r.ProductImageId).HasColumnName("product_image_id");
        builder.Property(r => r.RequestType).HasColumnName("request_type").HasConversion<string>().IsRequired();
        builder.Property(r => r.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(r => r.RequestedBy).HasColumnName("requested_by").IsRequired();

        // Postgres native array — Npgsql map thẳng List<Guid> <-> uuid[], không cần value converter.
        builder.Property(r => r.SourceImageIds).HasColumnName("source_image_ids").HasColumnType("uuid[]");

        builder.Property(r => r.MeshyTaskId).HasColumnName("meshy_task_id");
        builder.Property(r => r.AssignedStaffId).HasColumnName("assigned_staff_id");
        builder.Property(r => r.InternalFailureReason).HasColumnName("internal_failure_reason").HasConversion<string>();
        builder.Property(r => r.NextAttemptAt).HasColumnName("next_attempt_at");
        builder.Property(r => r.RejectedReason).HasColumnName("rejected_reason");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by");
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by");
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        // n–1: 1 product có nhiều request; mỗi request thuộc về một ảnh đích cụ thể.
        builder.HasIndex(r => r.ProductId);
        // Truy vấn worker (Initial: Queued/Processing) + hàng chờ staff (Regenerate: AwaitingStaff/InProgress).
        builder.HasIndex(r => r.Status);

        builder.HasOne(r => r.Product)
            .WithMany(p => p.Model3DRequests)
            .HasForeignKey(r => r.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.ProductImage)
            .WithMany()
            .HasForeignKey(r => r.ProductImageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(r => !r.IsDeleted);
    }
}
