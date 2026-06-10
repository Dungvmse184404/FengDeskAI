namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>
/// Trạng thái tổng của order (lưu DB dạng string). Là rollup của các delivery + thanh toán.
/// Lưu ý: State Diagram chưa chốt — tập giá trị này có thể điều chỉnh.
/// </summary>
public enum OrderStatus
{
    Pending,    // vừa tạo, chờ xử lý/thanh toán
    Paid,       // đã thanh toán
    Processing, // các nhà vườn đang chuẩn bị/giao
    Completed,  // tất cả delivery đã giao
    Cancelled,  // đã hủy
    Expired,    // quá hạn thanh toán (đơn online không trả tiền trong thời hạn)
}
