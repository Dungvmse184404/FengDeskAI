using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Sales.Services;

/// <summary>Quy tắc trạng thái dùng chung cho OrderService + PaymentService + ShippingService (webhook).</summary>
public static class OrderWorkflow
{
    /// <summary>
    /// Gom order.Items theo store thành các delivery (mỗi store một delivery, status Pending).
    /// Yêu cầu mỗi item đã nạp ProductItem.Product (để biết GardenStoreId).
    /// Gọi lúc checkout với đơn COD; với đơn online gọi khi webhook báo đã thanh toán.
    /// </summary>
    public static void GroupItemsIntoDeliveries(Order order)
    {
        foreach (var group in order.Items.GroupBy(i => i.ProductItem.Product.GardenStoreId))
        {
            var delivery = new Delivery
            {
                GardenStoreId = group.Key,
                Status = DeliveryStatus.Pending,
                ShippingFee = 0m,
                Subtotal = group.Sum(i => i.UnitPrice * i.Quantity),
            };
            foreach (var item in group)
            {
                item.Delivery = delivery;
                delivery.Items.Add(item);
            }
            order.Deliveries.Add(delivery);
        }
    }

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

    // Webhook nhà vận chuyển (GHN/AhaMove) có thể nhảy thẳng Confirmed → Shipped (bỏ Preparing),
    // báo DeliveryFailed, hoặc Returned ở nhiều mốc — nên cho phép các chuyển tiếp này.
    public static bool IsValidDeliveryTransition(DeliveryStatus from, DeliveryStatus to) => from switch
    {
        DeliveryStatus.Pending => to is DeliveryStatus.Confirmed or DeliveryStatus.Cancelled,
        DeliveryStatus.Confirmed => to is DeliveryStatus.Preparing or DeliveryStatus.Shipped
            or DeliveryStatus.Cancelled or DeliveryStatus.Returned,
        DeliveryStatus.Preparing => to is DeliveryStatus.Shipped or DeliveryStatus.Cancelled
            or DeliveryStatus.Returned,
        DeliveryStatus.Shipped => to is DeliveryStatus.Delivered or DeliveryStatus.DeliveryFailed
            or DeliveryStatus.Returned,
        DeliveryStatus.DeliveryFailed => to is DeliveryStatus.Shipped or DeliveryStatus.Returned
            or DeliveryStatus.Cancelled,
        DeliveryStatus.Delivered => to is DeliveryStatus.Returned,
        _ => false,
    };
}
