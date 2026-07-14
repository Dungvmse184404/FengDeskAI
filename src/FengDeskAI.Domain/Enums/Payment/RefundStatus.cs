namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>
/// Trạng thái một lệnh hoàn tiền (refund sub-saga, v2). Lưu DB dạng string.
/// Máy trạng thái ở <c>Domain.StateMachines.RefundStateMachine</c>.
/// QUAN TRỌNG: <see cref="Failed"/> KHÔNG phải trạng thái cuối — luôn có đường đi tiếp
/// (auto-retry → Processing, hết lượt → ManagerReview).
///
///   Pending       → Processing | Cancelled
///   Processing    → Completed | Failed
///   Failed        → Processing (retry) | ManagerReview
///   ManagerReview → Processing | Completed
/// (terminal: Completed, Cancelled)
/// </summary>
public enum RefundStatus
{
    Pending,       // đã tạo, chờ gọi cổng
    Processing,    // đã gọi cổng, chờ webhook xác nhận
    Completed,     // đã hoàn tiền cho khách
    Failed,        // gọi cổng/webhook thất bại (KHÔNG dead-end)
    ManagerReview, // hết lượt retry → chờ Manager can thiệp
    Cancelled,     // Manager hủy (phát hiện gian lận trước khi tiền đi)
}
