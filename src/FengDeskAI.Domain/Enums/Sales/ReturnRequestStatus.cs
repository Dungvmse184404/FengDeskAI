namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>
/// Trạng thái ticket RMA (v2). Lưu DB dạng string. Máy trạng thái đầy đủ ở
/// <c>Domain.StateMachines.ReturnStateMachine</c> — KHÔNG rẽ nhánh trạng thái ở nơi khác.
///
///   Requested        → NeedMoreEvidence | UnderReview | Cancelled
///   NeedMoreEvidence → Requested | Rejected
///   UnderReview      → Reviewing (plant_health) | ReturnInTransit (hàng vật lý)
///   ReturnInTransit  → ItemReceived
///   ItemReceived     → Reviewing
///   Reviewing        → Exchanging | Refunding | Rejected
///   Exchanging       → Completed | Refunding (fallback hết hàng)
///   Refunding        → Completed
/// (terminal: Completed, Cancelled, Rejected)
/// </summary>
public enum ReturnRequestStatus
{
    Requested,         // khách vừa tạo, chờ Staff tiếp nhận
    NeedMoreEvidence,  // Staff yêu cầu bổ sung bằng chứng (có deadline)
    UnderReview,       // Staff đã tiếp nhận; Vendor được thông báo (SLA phản hồi, non-blocking)
    ReturnInTransit,   // hàng vật lý đang được gửi trả về vendor
    ItemReceived,      // vendor đã nhận & xác nhận hàng trả (không quyết định kết quả)
    Reviewing,         // chờ Staff ra quyết định cuối
    Exchanging,        // Staff duyệt đổi hàng, đang tạo đơn thay thế
    Refunding,         // Staff duyệt hoàn tiền, refund saga đang chạy
    Completed,         // hoàn tất (đã hoàn tiền / đã giao hàng đổi)
    Cancelled,         // khách tự hủy (chỉ khi còn ở Requested)
    Rejected,          // bị từ chối (Staff hoặc quá deadline bổ sung bằng chứng)
}
