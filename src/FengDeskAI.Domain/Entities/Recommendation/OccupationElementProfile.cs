using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Một dòng trong <b>hồ sơ ngũ hành</b> của nghề: "nghề này cần hành X chiếm bao nhiêu phần".
/// 5 dòng cho một nghề, <c>Σ Share = 1</c>, không âm — cùng thang với <c>personalVector</c> /
/// <c>adjustedIdeal</c>.
///
/// <para>
/// Lưu <i>phân bố</i> chứ không lưu delta có dấu (như <see cref="WorkPurposeElementModifier"/>): chuyên
/// gia phát biểu nghề bằng "Kim 0.5 · Thủy 0.3 · …", đọc và duyệt được ngay; delta suy ra khi cần
/// (<c>δ = share − 0.2</c>, xem ADR <c>occupation-product-fit-v1.md</c> §2). Ràng buộc Σ=1 không CHECK
/// được ở mức dòng nên cưỡng chế ở service admin và seeder.
/// </para>
///
/// <para>
/// ⚠️ Hồ sơ nghề <b>không đổi được bản mệnh</b>: engine chặn hành khắc mệnh về ≤ 0 khi dựng trục nghề
/// (<c>OccupationAxis</c>) — dữ liệu được phép khai ý định, engine mới là nơi cưỡng chế kiêng kỵ.
/// </para>
/// </summary>
public class OccupationElementProfile : BaseEntity
{
    public Guid OccupationId { get; set; }
    public Occupation Occupation { get; set; } = null!;

    public FengShuiElement Element { get; set; }

    /// <summary>Tỉ trọng của hành trong nhu cầu của nghề (numeric(4,3)), ∈ [0, 1]; Σ theo nghề = 1.</summary>
    public decimal Share { get; set; }
}
