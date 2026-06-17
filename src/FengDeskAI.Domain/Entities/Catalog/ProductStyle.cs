namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ <see cref="Catalog.Style"/> (sản phẩm hợp với nhiều phong cách).
/// Junction thuần: composite key (product_id, style_code), không audit/soft-delete.
/// </summary>
public class ProductStyle
{
    public Guid ProductId { get; set; }
    public string StyleCode { get; set; } = null!;

    public Product Product { get; set; } = null!;
    public Style Style { get; set; } = null!;
}
