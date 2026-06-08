using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Đơn hàng của customer. Một order có thể gồm hàng từ nhiều nhà vườn → tách thành
/// nhiều <see cref="Delivery"/> (mỗi store một delivery), KHÔNG tách sub-order.
/// </summary>
public class Order : BaseEntity
{
    public Guid CustomerId { get; set; }
    public Guid ShippingAddressId { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public decimal Subtotal { get; set; }
    public decimal TotalShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public UserAddress ShippingAddress { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    public ICollection<OrderStatusLog> StatusLogs { get; set; } = new List<OrderStatusLog>();
}
