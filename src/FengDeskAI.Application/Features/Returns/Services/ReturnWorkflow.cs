using FengDeskAI.Domain.Entities.Sales;

namespace FengDeskAI.Application.Features.Returns.Services;

/// <summary>
/// Hằng số nghiệp vụ + helper thuần cho luồng RMA. Quy tắc chuyển trạng thái nằm ở
/// <c>Domain.StateMachines</c> (không lặp lại ở đây).
/// </summary>
public static class ReturnWorkflow
{
    /// <summary>Cửa sổ cho phép tạo ticket tính từ ngày giao thành công (ngày).</summary>
    public const int ReturnWindowDays = 7;

    /// <summary>SLA vendor phản hồi khi ticket vào UnderReview (giờ) — non-blocking.</summary>
    public const int VendorResponseSlaHours = 48;

    /// <summary>Hạn mặc định để khách bổ sung bằng chứng (giờ).</summary>
    public const int EvidenceSlaHours = 48;

    /// <summary>Hạn vendor dispute công nợ tính từ khi tạo (ngày).</summary>
    public const int LiabilityDisputeWindowDays = 7;

    /// <summary>Số lần auto-retry tối đa cho một refund thất bại.</summary>
    public const int MaxRefundRetries = 3;

    /// <summary>True nếu vẫn còn trong cửa sổ trả hàng (đã biết delivery đã Delivered).</summary>
    public static bool IsWithinWindow(DateTime? deliveredAtUtc, DateTime nowUtc)
        => (deliveredAtUtc ?? nowUtc).AddDays(ReturnWindowDays) >= nowUtc;

    /// <summary>Tổng giá trị các dòng trả (dùng cho số tiền hoàn — không bao giờ vượt giá trị này).</summary>
    public static decimal ComputeRefundAmount(IEnumerable<ReturnItem> items)
        => items.Sum(i => i.UnitPrice * i.Quantity);

    /// <summary>Khóa idempotency tất định theo ticket — 1 ticket chỉ 1 lệnh hoàn tiền (invariant #2).</summary>
    public static string RefundIdempotencyKey(Guid ticketId) => $"rma-refund-{ticketId:N}";
}
