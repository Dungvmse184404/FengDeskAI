namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>Trạng thái một lệnh hoàn tiền. Lưu DB dạng string.</summary>
public enum RefundStatus
{
    Pending,    // đã tạo, chờ xử lý
    Processing, // đang xử lý (đã gọi cổng / chờ chuyển khoản)
    Completed,  // đã hoàn tiền cho khách
    Failed,     // hoàn tiền thất bại
    Cancelled,  // hủy lệnh hoàn tiền
}
