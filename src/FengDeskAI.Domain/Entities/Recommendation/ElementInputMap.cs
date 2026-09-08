using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Bảng tra: một tín hiệu (màu/vật liệu/hình khối) đóng góp bao nhiêu vào từng hành.
/// Dùng chung cho cả phòng (workspace_profile_inputs) và sản phẩm (product_element_inputs).
/// Một (kind, code) có thể trải trên nhiều hành (vd SaltRock → Hỏa + Thổ). Seed sẵn, admin sửa.
/// </summary>
public class ElementInputMap : BaseEntity
{
    public ElementInputKind InputKind { get; set; }

    /// <summary>Mã tín hiệu bất biến, vd "Red", "Wood", "SaltRock", "Sphere".</summary>
    public string InputCode { get; set; } = null!;

    /// <summary>
    /// Nhãn tiếng Việt hiển thị cho user (vd Wood → "Gỗ"). Với tag do user tự tạo qua classifier,
    /// đây chính là chữ user đã gõ — dùng làm DẪN CHỨNG trong 3 dòng nhận định &amp; tooltip radar.
    /// Null với row cũ chưa backfill → FE/BE fallback về <see cref="InputCode"/>.
    /// </summary>
    public string? LabelVi { get; set; }

    /// <summary>
    /// Phạm vi hiển thị: <see cref="ElementInputVisibility.Pending"/> (chờ admin xem) /
    /// <see cref="ElementInputVisibility.Personal"/> (admin đã xem, giữ riêng cho người tạo) /
    /// <see cref="ElementInputVisibility.Public"/> (tag chính thức, mọi user dùng).
    /// Chỉ lọc ở tầng khám phá — xem chú thích trên enum.
    /// </summary>
    public ElementInputVisibility Visibility { get; set; } = ElementInputVisibility.Pending;

    /// <summary>Tiện ích đọc: tag đã là tag chung của hệ thống.</summary>
    public bool IsPublic => Visibility == ElementInputVisibility.Public;

    /// <summary>
    /// Tag này có hiện trong picker / prompt AI của <paramref name="userId"/> không.
    /// Một chỗ duy nhất định nghĩa quy tắc khám phá — mọi service gọi lại đây, tránh lệch nhau.
    /// </summary>
    public bool IsVisibleTo(Guid userId) => IsPublic || CreatedBy == userId;

    public FengShuiElement Element { get; set; }

    /// <summary>Trọng số đóng góp vào hành (numeric(4,3)), mặc định 1.0.</summary>
    public decimal Weight { get; set; } = 1.0m;
}
