namespace FengDeskAI.Domain.Enums.Catalog;

/// <summary>
/// Mục tiêu phong thủy người dùng nêu trong MỘT phiên tư vấn ("tôi muốn vật phẩm hỗ trợ tiền tài").
/// <para>
/// KHÔNG lưu trên <c>User</c>: ý định đổi theo bối cảnh — cùng một người cần tài lộc ở văn phòng và
/// sức khỏe ở nhà. Đây là tham số runtime, truyền qua request/tool và dùng để LỌC ứng viên trước khi chấm.
/// </para>
/// Năm giá trị ứng với 4 cung tốt Bát Trạch (Sinh Khí / Thiên Y / Diên Niên / Phục Vị) — cố định,
/// admin không thêm được, nên là enum chứ không phải bảng tra cứu như <c>Vibe</c>/<c>Style</c>.
/// Xem <c>docs/adr/personalized-recommendation-v3.1.md</c> §4.
/// </summary>
public enum Aspiration
{
    /// <summary>Tài lộc — cung Sinh Khí.</summary>
    Wealth,

    /// <summary>Công danh, thăng tiến — cung Sinh Khí.</summary>
    Career,

    /// <summary>Sức khỏe, phục hồi — cung Thiên Y.</summary>
    Health,

    /// <summary>Quan hệ, hòa hợp — cung Diên Niên.</summary>
    Relationship,

    /// <summary>Học hành, thi cử, tập trung — cung Phục Vị.</summary>
    Study,
}
