using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Dòng hàng trong order. Snapshot tên + đơn giá tại thời điểm đặt để lịch sử đơn
/// không đổi khi catalog thay đổi. Store của dòng suy ra qua <see cref="Delivery"/>.
/// </summary>
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProductItemId { get; set; }
    public Guid DeliveryId { get; set; }

    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    public Order Order { get; set; } = null!;
    public Delivery Delivery { get; set; } = null!;
    public ProductItem ProductItem { get; set; } = null!;
}
