namespace FengDeskAI.Domain.Enums.Payment;

/// <summary>Trạng thái giao dịch thanh toán (lưu DB string).</summary>
public enum PaymentStatus
{
    Pending,    // đã tạo link, chờ thanh toán
    Paid,       // thanh toán thành công
    Cancelled,  // khách hủy
    Failed,     // thất bại
    Expired,    // hết hạn link
}
