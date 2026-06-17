using FengDeskAI.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FengDeskAI.Infrastructure.Persistence.Configurations;

public class ElementConfiguration : IEntityTypeConfiguration<Element>
{
    public void Configure(EntityTypeBuilder<Element> builder)
    {
        builder.ToTable("elements");

        builder.HasKey(e => e.Code);
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(10);
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").HasDefaultValue(0);

        // FK từ product_element.element / workspace_profiles.feng_shui_element / feng_shui_rules.{subject,object}_element
        // tới elements.code được thêm bằng raw SQL trong migration (các cột đó vẫn là enum-string ở CLR,
        // engine ngũ hành giữ nguyên) kèm ON UPDATE CASCADE.
    }
}
