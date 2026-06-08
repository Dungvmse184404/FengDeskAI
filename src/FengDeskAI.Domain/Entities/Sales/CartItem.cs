using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>Một dòng trong giỏ — tham chiếu tới một product item (SKU) + số lượng.</summary>
public class CartItem : BaseEntity
{
    public Guid CartId { get; set; }
    public Guid ProductItemId { get; set; }
    public int Quantity { get; set; }
    public DateTime AddedAt { get; set; }

    public Cart Cart { get; set; } = null!;
    public ProductItem ProductItem { get; set; } = null!;
}
