namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>
/// Trạng thái fulfillment của một delivery (phần hàng của 1 store trong order).
/// Lưu DB dạng string. State Diagram chưa chốt — có thể điều chỉnh.
/// </summary>
public enum DeliveryStatus
{
    Pending,    // chờ store xác nhận
    Confirmed,  // store đã xác nhận
    Preparing,  // đang chuẩn bị hàng
    Shipped,    // đã bàn giao vận chuyển
    Delivered,  // đã giao thành công
    DeliveryFailed,
    Cancelled,  // đã hủy
    Returned,   // hoàn hàng
}
