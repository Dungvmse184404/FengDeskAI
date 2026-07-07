namespace FengDeskAI.Domain.Enums.Workspace;

/// <summary>
/// Loại tín hiệu vật lý dùng để suy ra vector ngũ hành cho phòng &amp; sản phẩm:
/// màu sắc, chất liệu, hình khối. Map cụ thể nằm trong bảng <c>element_input_map</c>.
/// </summary>
public enum ElementInputKind
{
    Color,
    Material,
    Shape,
}
