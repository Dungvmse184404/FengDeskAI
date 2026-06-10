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

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductItemId { get; set; }

    /// <summary>Null khi đơn online chưa thanh toán (delivery chưa được tạo).</summary>
    public Guid? DeliveryId { get; set; }
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class DeliveryResponse
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
}

public class OrderStatusLogResponse
{
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class OrderListItemResponse
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TotalShippingFee { get; set; }
    public decimal TotalAmount { get; set; }
    public int DeliveryCount { get; set; }
    public DateTime CreatedAt { get; set; }
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
    public DeliveryStatus Status { get; set; }
    public decimal ShippingFee { get; set; }
    public decimal Subtotal { get; set; }
    public string? TrackingCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateDeliveryStatusRequest
{
    public DeliveryStatus Status { get; set; }
    public string? TrackingCode { get; set; }
    public string? ShippingProvider { get; set; }
    public string? Note { get; set; }
}
