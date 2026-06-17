using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng nối Product ↔ <see cref="WorkspaceStyle"/> (sản phẩm hợp với nhiều phong cách).
/// Junction thuần: composite key (product_id, style), không audit/soft-delete.
/// </summary>
public class ProductStyle
{
    public Guid ProductId { get; set; }
    public WorkspaceStyle Style { get; set; }

    public Product Product { get; set; } = null!;
}
