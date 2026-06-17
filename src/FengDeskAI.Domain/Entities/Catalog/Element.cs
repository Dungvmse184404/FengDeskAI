namespace FengDeskAI.Domain.Entities.Catalog;

/// <summary>
/// Bảng tra cứu ngũ hành (Kim/Mộc/Thủy/Hỏa/Thổ). <see cref="Code"/> = mã canonical mà engine
/// phong thủy tham chiếu (cố định 5 hành); <see cref="Name"/> là tên hiển thị, sửa tự do.
/// Khác Style/Vibe: KHÔNG thêm hành mới (ngũ hành cố định) — chỉ sửa tên hiển thị.
/// </summary>
public class Element : ILookup
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
