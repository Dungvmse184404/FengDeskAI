using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>Quy tắc trạng thái + điều kiện trả hàng dùng chung cho ReturnService.</summary>
public static class ReturnWorkflow
{
    /// <summary>Cửa sổ cho phép yêu cầu trả hàng tính từ ngày giao thành công (ngày). Mặc định 7 ngày.</summary>
    public const int ReturnWindowDays = 7;

    /// <summary>Delivery đủ điều kiện trả khi đã giao thành công và còn trong cửa sổ trả hàng.</summary>
    public static bool IsDeliveryReturnable(Delivery delivery, DateTime nowUtc)
    {
        if (delivery.Status != DeliveryStatus.Delivered) return false;
        var deliveredAt = delivery.DeliveredAt ?? delivery.UpdatedAt;
        return deliveredAt.AddDays(ReturnWindowDays) >= nowUtc;
    }

    /// <summary>True nếu vẫn còn trong cửa sổ trả hàng (đã biết delivery đã Delivered).</summary>
    public static bool IsWithinWindow(DateTime? deliveredAtUtc, DateTime nowUtc)
        => (deliveredAtUtc ?? nowUtc).AddDays(ReturnWindowDays) >= nowUtc;

    public static decimal ComputeRefundAmount(IEnumerable<ReturnItem> items)
        => items.Sum(i => i.UnitPrice * i.Quantity);

    public static bool IsValidReturnTransition(ReturnRequestStatus from, ReturnRequestStatus to) => from switch
    {
        ReturnRequestStatus.Requested => to is ReturnRequestStatus.Approved
            or ReturnRequestStatus.Rejected or ReturnRequestStatus.Cancelled,
        ReturnRequestStatus.Approved => to is ReturnRequestStatus.ReturnInTransit
            or ReturnRequestStatus.Cancelled,
        ReturnRequestStatus.ReturnInTransit => to is ReturnRequestStatus.ItemReceived,
        ReturnRequestStatus.ItemReceived => to is ReturnRequestStatus.Refunding
            or ReturnRequestStatus.Exchanging or ReturnRequestStatus.Rejected,
        ReturnRequestStatus.Refunding => to is ReturnRequestStatus.Completed,
        ReturnRequestStatus.Exchanging => to is ReturnRequestStatus.Completed,
        _ => false,
    };
}
