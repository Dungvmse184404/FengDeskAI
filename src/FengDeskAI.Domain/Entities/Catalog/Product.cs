using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Vendor;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Sản phẩm (mặt hàng cha) thuộc một garden store. Giá + tồn kho nằm ở các
/// <see cref="ProductItem"/> (biến thể/SKU). Thuộc tính phong thủy đánh dấu qua tags.
/// </summary>
public class Product : BaseEntity
{
    public Guid GardenStoreId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public GardenStore Store { get; set; } = null!;
    public ICollection<ProductItem> Items { get; set; } = new List<ProductItem>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}
