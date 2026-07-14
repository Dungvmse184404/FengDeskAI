namespace FengDeskAI.Application.Features.Workspace.DTOs;

/// <summary>
/// Từ vựng màu/vật liệu/vật trang trí hợp lệ (khớp <c>element_input_map</c>) — cho FE dựng tag picker
/// "hiện trạng phòng hiện tại" ở form tạo/sửa workspace. Chỉ trả mã (đã là tiếng Anh, hiển thị thẳng).
/// Riêng cho workspace — KHÔNG dùng chung với vocabulary sản phẩm (không có Shape, có DecorItem).
/// </summary>
public sealed record ElementInputVocabularyResponse(
    List<string> Colors,
    List<string> Materials,
    List<string> DecorItems);
