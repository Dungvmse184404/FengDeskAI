using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Sales.DTOs;

public class CheckoutItemRequest
{
    public Guid ProductItemId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class CheckoutRequest
{
    /// <summary>Địa chỉ giao. Bỏ trống / Guid.Empty = dùng địa chỉ mặc định của user.</summary>
    public Guid? ShippingAddressId { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Sản phẩm cần đặt (productItemId + quantity). Bỏ trống = đặt toàn bộ giỏ hàng.
    /// Món trùng giỏ sẽ bị xóa khỏi giỏ sau khi đặt; món không có trong giỏ vẫn đặt được.
    /// </summary>
    public List<CheckoutItemRequest>? Items { get; set; }

    /// <summary>PayOS (mặc định): thanh toán online, quá 15' không trả tiền đơn sẽ hết hạn.
    /// COD: thanh toán khi nhận hàng, delivery tạo ngay khi đặt.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.PayOS;
}

/// <summary>Kết quả xem trước phí ship (FE gọi trước khi đặt hàng) — không tạo đơn.</summary>
public class ShippingFeePreviewResponse
{
    public decimal Subtotal { get; set; }
    public decimal TotalShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public List<StoreShippingFeeResponse> Stores { get; set; } = new();
}

public class StoreShippingFeeResponse
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal ShippingFee { get; set; }
}

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductItemId { get; set; }

    /// <summary>Id sản phẩm gốc (Product) của biến thể — FE dùng để đánh giá / mở trang sản phẩm.</summary>
    public Guid ProductId { get; set; }

    /// <summary>Null khi đơn online chưa thanh toán (delivery chưa được tạo).</summary>
    public Guid? DeliveryId { get; set; }
    public string ProductName { get; set; } = null!;

    /// <summary>Tên biến thể tại thời điểm hiển thị (vd "Đỏ / Size L"). Null nếu sản phẩm không có biến thể đặt tên.</summary>
    public string? VariantName { get; set; }

    /// <summary>Ảnh đại diện của sản phẩm (SortOrder nhỏ nhất). Null khi sản phẩm chưa có ảnh.</summary>
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class DeliveryResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public string? StoreName { get; set; }
    public DeliveryStatus Status { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Subtotal { get; set; }
    public string? TrackingCode { get; set; }
    public string? ShippingProvider { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
}

public class OrderStatusLogResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>Cửa hàng có hàng trong đơn — FE hiển thị tên shop trên thẻ đơn hàng và link sang trang store.</summary>
public class OrderStoreResponse
{
    public Guid StoreId { get; set; }
    public string? StoreName { get; set; }
}

public class OrderListItemResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public int DeliveryCount { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Lấy từ delivery; đơn online chưa thanh toán (chưa có delivery) thì suy ra từ sản phẩm trong đơn.</summary>
    public List<OrderStoreResponse> Stores { get; set; } = new();

    /// <summary>Sản phẩm trong đơn — FE hiển thị ngay trên thẻ đơn ở danh sách, khỏi gọi chi tiết từng đơn.</summary>
    public List<OrderItemResponse> Items { get; set; } = new();
}

public class OrderDetailResponse
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid ShippingAddressId { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
    public List<DeliveryResponse> Deliveries { get; set; } = new();
    public List<OrderStatusLogResponse> StatusLogs { get; set; } = new();
}

/// <summary>Delivery hiển thị ở màn vendor (kèm orderId).</summary>
public class StoreDeliveryResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public DeliveryStatus Status { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Subtotal { get; set; }
    public string? TrackingCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class AssignDeliveryStaffRequest
{
    public Guid StaffId { get; set; }
}

public class UpdateDeliveryStatusRequest
{
    public DeliveryStatus Status { get; set; }
    public string? TrackingCode { get; set; }
    public string? ShippingProvider { get; set; }
    public string? Note { get; set; }
}

/// <summary>Địa chỉ giao — chỉ các field vendor cần để đóng gói/in vận đơn, không lộ toàn bộ UserAddress.</summary>
public class DeliveryShippingAddressResponse
{
    public string RecipientName { get; set; } = null!;
    public string RecipientPhone { get; set; } = null!;
    public string StreetAddress { get; set; } = null!;
    public string FullAddressText { get; set; } = null!;
}

/// <summary>
/// Chi tiết một đơn giao (delivery) — màn vendor (garden owner/staff) xem để đóng gói.
/// Chỉ trả sản phẩm + thông tin thuộc đúng delivery này (không lộ hàng của store khác trong cùng order).
/// </summary>
public class DeliveryOrderDetailResponse
{
    public Guid Id { get; set; }
    public Guid GardenStoreId { get; set; }
    public string? StoreName { get; set; }
    public DeliveryStatus Status { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Subtotal { get; set; }
    public string? TrackingCode { get; set; }
    public string? ShippingProvider { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }

    public Guid OrderId { get; set; }
    public DateTime OrderCreatedAt { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public string? OrderNote { get; set; }

    public List<OrderItemResponse> Items { get; set; } = new();
    public DeliveryShippingAddressResponse ShippingAddress { get; set; } = null!;
}
