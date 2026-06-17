namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng tra cứu cảm giác/không khí sản phẩm tạo ra (Focus, Calm, Energize...).
/// <see cref="Code"/> bất biến (thuật toán + map từ WorkPurpose tham chiếu); <see cref="Name"/> sửa tự do.
/// </summary>
public class Vibe : ILookup
{
    /// <summary>Khoá tự nhiên bất biến, vd "Focus".</summary>
    public string Code { get; set; } = null!;

    /// <summary>Tên hiển thị (có thể đổi), vd "Tập trung".</summary>
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
