namespace FengDeskAI.Domain.Enums.Workspace;

/// <summary>
/// Loại tín hiệu vật lý dùng để suy ra vector ngũ hành cho phòng &amp; sản phẩm:
/// màu sắc, chất liệu, hình khối, vật trang trí cụ thể. Map cụ thể nằm trong bảng
/// <c>element_input_map</c>. DecorItem chủ yếu dùng cho workspace (bể cá, cây xanh...) —
/// sản phẩm hiếm khi cần vì đã có <c>product_elements</c> primary/secondary riêng.
/// </summary>
public enum ElementInputKind
{
    Color,
    Material,
    Shape,
    DecorItem,
}
