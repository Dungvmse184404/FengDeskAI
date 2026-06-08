using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>Ảnh của một <see cref="Product"/>.</summary>
public class ProductImage : BaseEntity
{
    public Guid ProductId { get; set; }
    public string Url { get; set; } = null!;
    public int SortOrder { get; set; }

    public Product Product { get; set; } = null!;
}
