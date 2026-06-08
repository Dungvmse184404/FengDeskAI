using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Shipping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class DeliveryProgressLogConfiguration : IEntityTypeConfiguration<DeliveryProgressLog>
{
    public void Configure(EntityTypeBuilder<DeliveryProgressLog> builder)
    {
        builder.ToTable("delivery_progress_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.DeliveryId).HasColumnName("delivery_id").IsRequired();
        builder.Property(l => l.SourceType).HasColumnName("source_type").HasConversion<int>();
        builder.Property(l => l.FromStatus).HasColumnName("from_status").HasMaxLength(50);
        builder.Property(l => l.ToStatus).HasColumnName("to_status").HasMaxLength(50);
        builder.Property(l => l.RawPayload).HasColumnName("raw_payload").HasColumnType("jsonb");
        builder.Property(l => l.Note).HasColumnName("note");
        builder.Property(l => l.LoggedAt).HasColumnName("logged_at");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        // audit UpdatedBy = "ai cập nhật" (diagram updated_by); null khi webhook ẩn danh
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(l => l.DeliveryId);

        builder.HasOne(l => l.Delivery)
            .WithMany(d => d.ProgressLogs)
            .HasForeignKey(l => l.DeliveryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
