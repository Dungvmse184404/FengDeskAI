namespace FengDeskAI.Domain.Enums.Workspace;

/// <summary>
/// Phạm vi hiển thị của một tag trong <c>element_input_map</c>.
/// <para>
/// <b>Pending</b> và <b>Personal</b> hiển thị GIỐNG HỆT nhau (chỉ người tạo thấy) — khác biệt duy nhất
/// là <b>đã được admin xem hay chưa</b>. Nhờ vậy hàng đợi duyệt (= Pending) luôn rút được về 0,
/// thay vì phình mãi vì những tag admin đã xem nhưng cố ý không duyệt.
/// </para>
/// <para>
/// Lưu ý: phạm vi này CHỈ áp ở tầng KHÁM PHÁ (picker hiện trạng, prompt AI intake, form gắn tag sản
/// phẩm). Tầng SỬ DỤNG (resolver, chấm điểm, validate khi lưu workspace) KHÔNG lọc — nếu lọc, tag
/// riêng của user sẽ biến mất khỏi đồ thị ngũ hành của chính họ.
/// </para>
/// </summary>
public enum ElementInputVisibility
{
    /// <summary>User vừa tạo ở bước intake, admin CHƯA xem. Nằm trong hàng đợi duyệt.</summary>
    Pending = 0,

    /// <summary>Admin đã xem và quyết định giữ riêng (tag quá đặc thù). Ra khỏi hàng đợi, vẫn dùng được.</summary>
    Personal = 1,

    /// <summary>Tag chính thức: seed hệ thống, admin tự thêm, hoặc tag user đã được duyệt.</summary>
    Public = 2,
}
