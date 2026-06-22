using FengDeskAI.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ReturnRequestImageConfiguration : IEntityTypeConfiguration<ReturnRequestImage>
{
    public void Configure(EntityTypeBuilder<ReturnRequestImage> builder)
    {
        builder.ToTable("return_request_images");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ReturnRequestId).HasColumnName("return_request_id").IsRequired();
        builder.Property(i => i.ImageUrl).HasColumnName("image_url").IsRequired();
        builder.Property(i => i.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);

        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");
        builder.Property(i => i.CreatedBy).HasColumnName("created_by");
        builder.Property(i => i.UpdatedBy).HasColumnName("updated_by");
        builder.Property(i => i.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);

        builder.HasIndex(i => i.ReturnRequestId);

        builder.HasQueryFilter(i => !i.IsDeleted);
    }
}
