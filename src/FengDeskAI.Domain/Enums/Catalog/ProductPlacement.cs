namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>
/// Vị trí/cách sử dụng vật phẩm — quyết định ENGINE CHẤM ĐIỂM THẾ NÀO, không chỉ là phân loại hiển thị.
/// Xem ma trận luật ở <c>docs/adr/product-placement-personal-recommendation.md</c>.
/// <para>
/// <b>Đã bỏ <c>Architectural</c></b> (28/08/2026): nó chấm y hệt <see cref="Desk"/> — một nhánh luật
/// trùng lặp chỉ tồn tại để chờ tính năng "hướng bắt buộc của vật trấn yểm" (gương bát quái phải chiếu
/// ra ngoài) vốn chưa làm. Khi nào làm thì thêm lại KÈM cột hướng bắt buộc trên <c>products</c> —
/// đừng thêm lại giá trị enum suông, nó chỉ nhân đôi nhánh test mà không đổi hành vi.
/// </para>
/// </summary>
public enum ProductPlacement
{
    /// <summary>
    /// Đồ đặt trong không gian (mặc định): chấm theo gap của phòng + có gợi ý hướng đặt.
    /// Gồm cả vật trấn yểm gắn kiến trúc (gương bát quái, chuông gió treo cửa) — xem ghi chú ở đầu file.
    /// </summary>
    Desk,

    /// <summary>Cây/vật sống: vẫn chấm theo gap phòng nhưng KHÔNG xét hướng (đặt theo ánh sáng, không theo la bàn).</summary>
    Living,

    /// <summary>Mang theo người (đeo tay, mặt dây, để ví, treo xe): chấm theo bản mệnh, bỏ hẳn gap phòng &amp; hướng.</summary>
    Carry,

    /// <summary>Hàng tiêu hao (nhang, nến, muối): KHÔNG đưa vào bất kỳ luồng gợi ý nào — vẫn tìm &amp; mua bình thường.</summary>
    Consumable,
}
