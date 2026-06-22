namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>
/// Trạng thái yêu cầu trả hàng/đổi trả (RMA). Lưu DB dạng string.
/// Máy trạng thái:
///   Requested      → Approved | Rejected | Cancelled
///   Approved       → ReturnInTransit | Cancelled
///   ReturnInTransit→ ItemReceived
///   ItemReceived   → Refunding | Exchanging | Rejected
///   Refunding      → Completed
///   Exchanging     → Completed
/// (terminal: Completed, Rejected, Cancelled)
/// </summary>
public enum ReturnRequestStatus
{
    Requested,        // khách vừa tạo, chờ người bán duyệt
    Approved,         // đã duyệt, chờ khách gửi hàng trả về
    Rejected,         // bị từ chối (trước hoặc sau khi kiểm hàng)
    ReturnInTransit,  // khách đã gửi hàng trả, đang vận chuyển về
    ItemReceived,     // người bán đã nhận hàng trả, đang kiểm
    Refunding,        // đã chấp nhận hoàn tiền, đang xử lý hoàn tiền
    Exchanging,       // đã chấp nhận đổi hàng, đang giao hàng thay thế
    Completed,        // hoàn tất (đã hoàn tiền / đã giao hàng đổi)
    Cancelled,        // khách tự hủy yêu cầu
}
