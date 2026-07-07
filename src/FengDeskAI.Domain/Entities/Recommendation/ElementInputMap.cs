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

    public FengShuiElement Element { get; set; }

    /// <summary>Trọng số đóng góp vào hành (numeric(4,3)), mặc định 1.0.</summary>
    public decimal Weight { get; set; } = 1.0m;
}
