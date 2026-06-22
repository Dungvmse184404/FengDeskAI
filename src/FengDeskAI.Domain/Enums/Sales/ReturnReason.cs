namespace FengDeskAI.Domain.Enums.Sales;

/// <summary>Lý do trả hàng/đổi trả. Lưu DB dạng string.</summary>
public enum ReturnReason
{
    Defective,        // sản phẩm lỗi/hỏng
    WrongItem,        // giao sai sản phẩm
    NotAsDescribed,   // không đúng mô tả
    DamagedInTransit, // hư hại khi vận chuyển
    ChangedMind,      // khách đổi ý (không do lỗi người bán)
    Other,            // lý do khác
}
