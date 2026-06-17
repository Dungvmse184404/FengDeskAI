namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ <see cref="Catalog.Vibe"/> (một sản phẩm có thể tạo nhiều cảm giác).
/// Junction thuần: composite key (product_id, vibe_code), không audit/soft-delete.
/// </summary>
public class ProductVibe
{
    public Guid ProductId { get; set; }
    public string VibeCode { get; set; } = null!;

    public Product Product { get; set; } = null!;
    public Vibe Vibe { get; set; } = null!;
}
