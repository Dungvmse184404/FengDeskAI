using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Shipping;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>
/// Đơn vị giao hàng = phần hàng của một garden store trong một order.
/// Mỗi store trong order có một delivery với status fulfillment riêng.
/// </summary>
public class Delivery : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid GardenStoreId { get; set; }
    public Guid? AssignedStaffId { get; set; }

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public string? TrackingCode { get; set; }
    public string? ProviderOrderId { get; set; }
    public string? ShippingProvider { get; set; }
    /// <summary>Link theo dõi đơn của nhà vận chuyển (vd shared_link của AhaMove).</summary>
    public string? TrackingUrl { get; set; }

    public decimal ShippingFee { get; set; }
    public decimal Subtotal { get; set; }

    /// <summary>Đơn giao hàng thay thế do đổi trả (RMA) — giá trị 0đ, gửi từ garden gốc cho khách.</summary>
    public bool IsExchange { get; set; }

    public DateTime? AssignedAt { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    /// <summary>
    /// Đã cộng tiền hàng vào số dư chủ vườn lúc nào (null = chưa). Cờ này là thứ DUY NHẤT chặn cộng hai
    /// lần: worker quét lại mỗi chu kỳ, không có mốc này thì mỗi lượt quét lại cộng thêm một lần nữa.
    /// </summary>
    public DateTime? PayoutCreditedAt { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }

    public Order Order { get; set; } = null!;
    public GardenStore Store { get; set; } = null!;
    public User? AssignedStaff { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<DeliveryProgressLog> ProgressLogs { get; set; } = new List<DeliveryProgressLog>();
}
