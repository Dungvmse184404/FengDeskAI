namespace FengDeskAI.Domain.Enums.Shipping;

/// <summary>
/// Nguồn tạo ra một bản ghi tiến trình giao hàng (lưu DB dạng int — diagram source_type INTEGER).
/// </summary>
public enum DeliverySource
{
    Manual = 0,  // nhân viên store cập nhật tay
    Webhook = 1, // callback từ nhà vận chuyển
    System = 2,  // hệ thống tự động (vd rollup)
}
