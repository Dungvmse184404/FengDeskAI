using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Enums.Payment;
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

    /// <summary>Phương thức thanh toán chọn lúc checkout — quyết định thời điểm tạo delivery
    /// (COD: ngay khi đặt; online: khi webhook báo đã thanh toán) và đơn có bị hết hạn không.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayOS;

    public decimal Subtotal { get; set; }
    public decimal TotalShippingFee { get; set; }

    /// <summary>
    /// Tổng giảm phí ship từ voucher. Khách trả
    /// <c>TotalAmount = Subtotal + TotalShippingFee − ShippingDiscount − PlatformItemDiscount − SellerItemDiscount</c>.
    /// </summary>
    public decimal ShippingDiscount { get; set; }

    /// <summary>Giảm TIỀN HÀNG do SÀN tài trợ (trừ vào hoa hồng sàn, nhà vườn vẫn nhận đủ).</summary>
    public decimal PlatformItemDiscount { get; set; }

    /// <summary>Giảm TIỀN HÀNG do NHÀ VƯỜN tài trợ (trừ thẳng vào tiền họ nhận, hoa hồng sàn giữ nguyên).</summary>
    public decimal SellerItemDiscount { get; set; }

    /// <summary>Mã voucher đã áp (chụp lại để hiển thị); chi tiết lượt dùng ở <c>voucher_redemptions</c>.</summary>
    public string? VoucherCode { get; set; }

    /// <summary>
    /// Tỉ lệ phí sàn chốt lúc đặt — cũng là tỉ lệ đã dùng để tính trần voucher. Mọi delivery của đơn (kể cả
    /// delivery sinh muộn lúc webhook PayOS) lấy đúng số này, nên Manager đổi phí giữa chừng không làm lệch đơn.
    /// </summary>
    public decimal CommissionRate { get; set; }

    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }

    public UserAddress ShippingAddress { get; set; } = null!;
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
    public ICollection<OrderStoreCharge> StoreCharges { get; set; } = new List<OrderStoreCharge>();
    public ICollection<OrderStatusLog> StatusLogs { get; set; } = new List<OrderStatusLog>();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? StatusChangeNote { get; set; }
}
