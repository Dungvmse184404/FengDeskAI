using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ <see cref="Vibe"/> (một sản phẩm có thể tạo nhiều cảm giác).
/// Junction thuần: composite key (product_id, vibe), không audit/soft-delete.
/// </summary>
public class ProductVibe
{
    public Guid ProductId { get; set; }
    public Vibe Vibe { get; set; }

    public Product Product { get; set; } = null!;
}
