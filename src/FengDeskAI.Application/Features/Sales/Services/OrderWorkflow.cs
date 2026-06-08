using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Sales.Services;

/// <summary>Quy tắc trạng thái dùng chung cho OrderService + ShippingService (webhook).</summary>
public static class OrderWorkflow
{
    /// <summary>Suy ra trạng thái order tổng từ trạng thái các delivery (rollup).</summary>
    public static OrderStatus ComputeOrderStatus(IReadOnlyCollection<DeliveryStatus> deliveryStatuses)
    {
        if (deliveryStatuses.Count == 0) return OrderStatus.Pending;
        if (deliveryStatuses.All(s => s == DeliveryStatus.Delivered)) return OrderStatus.Completed;
        if (deliveryStatuses.All(s => s == DeliveryStatus.Cancelled)) return OrderStatus.Cancelled;
        if (deliveryStatuses.Any(s => s is DeliveryStatus.Confirmed or DeliveryStatus.Preparing
                or DeliveryStatus.Shipped or DeliveryStatus.Delivered))
            return OrderStatus.Processing;
        return OrderStatus.Pending;
    }

    public static bool IsValidDeliveryTransition(DeliveryStatus from, DeliveryStatus to) => from switch
    {
        DeliveryStatus.Pending => to is DeliveryStatus.Confirmed or DeliveryStatus.Cancelled,
        DeliveryStatus.Confirmed => to is DeliveryStatus.Preparing or DeliveryStatus.Cancelled,
        DeliveryStatus.Preparing => to is DeliveryStatus.Shipped or DeliveryStatus.Cancelled,
        DeliveryStatus.Shipped => to is DeliveryStatus.Delivered or DeliveryStatus.Returned,
        DeliveryStatus.Delivered => to is DeliveryStatus.Returned,
        _ => false,
    };
}
