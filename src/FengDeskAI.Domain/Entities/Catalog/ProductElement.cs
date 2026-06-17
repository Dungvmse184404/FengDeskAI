using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ <see cref="FengShuiElement"/> (một sản phẩm có thể mang nhiều hành).
/// Junction thuần: composite key (product_id, element), không audit/soft-delete.
/// <see cref="IsPrimary"/> = hành chính (đúng một dòng/sản phẩm); còn lại là hành phụ.
/// </summary>
public class ProductElement
{
    public Guid ProductId { get; set; }
    public FengShuiElement Element { get; set; }
    public bool IsPrimary { get; set; }

    public Product Product { get; set; } = null!;
}
