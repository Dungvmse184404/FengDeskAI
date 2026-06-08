using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Sales.DTOs;

public class CheckoutRequest
{
    public Guid ShippingAddressId { get; set; }
    public string? Note { get; set; }
}

public class OrderItemResponse
{
    public Guid Id { get; set; }
    public Guid ProductItemId { get; set; }
    public Guid DeliveryId { get; set; }
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
