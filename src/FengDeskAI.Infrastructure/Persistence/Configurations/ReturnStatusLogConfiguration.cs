using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ReturnStatusLogConfiguration : IEntityTypeConfiguration<ReturnStatusLog>
{
    public void Configure(EntityTypeBuilder<ReturnStatusLog> builder)
    {
        builder.ToTable("return_status_logs");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.ReturnRequestId).HasColumnName("return_request_id").IsRequired();
        builder.Property(l => l.FromStatus).HasColumnName("from_status").HasMaxLength(30);
        builder.Property(l => l.ToStatus).HasColumnName("to_status").HasMaxLength(30).IsRequired();
        builder.Property(l => l.ChangedBy).HasColumnName("changed_by");
        builder.Property(l => l.Note).HasColumnName("note");
        builder.Property(l => l.ChangedAt).HasColumnName("changed_at");

        builder.Property(l => l.CreatedAt).HasColumnName("created_at");
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by");
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by");
        builder.Property(l => l.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(l => l.ReturnRequestId);

        builder.HasQueryFilter(l => !l.IsDeleted);
    }
}
