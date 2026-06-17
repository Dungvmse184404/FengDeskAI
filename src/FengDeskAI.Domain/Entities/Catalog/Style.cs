namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng tra cứu phong cách không gian (Modern, Minimal, Scandinavian...).
/// <see cref="Code"/> bất biến (khoá máy + thuật toán tham chiếu); <see cref="Name"/> sửa tự do.
/// Admin thêm phong cách mới = thêm 1 dòng, không cần đụng code.
/// </summary>
public class Style : ILookup
{
    /// <summary>Khoá tự nhiên bất biến, vd "Minimal".</summary>
    public string Code { get; set; } = null!;

    /// <summary>Tên hiển thị (có thể đổi), vd "Tối giản".</summary>
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
