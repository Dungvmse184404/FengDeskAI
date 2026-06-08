using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Nhãn tự do gắn cho sản phẩm (vd: "Thủy", "Giảm căng thẳng", "Trang trọng").
/// Dùng đánh dấu thuộc tính phong thủy/công dụng — input cho AI matching.
/// </summary>
public class Tag : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
