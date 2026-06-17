using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Luật ngũ hành dạng dữ liệu (data-driven thay vì hardcode): xét từ mệnh người dùng
/// (<see cref="SubjectElement"/>) tới hành sản phẩm (<see cref="ObjectElement"/>) →
/// quan hệ + mức điểm đóng góp vào <c>elementMatch</c>. Seed 25 cặp (5×5).
/// Admin có thể tinh chỉnh điểm mà không cần đổi code.
/// </summary>
public class FengShuiRule : BaseEntity
{
    /// <summary>Mệnh người dùng.</summary>
    public FengShuiElement SubjectElement { get; set; }

    /// <summary>Hành của sản phẩm được xét.</summary>
    public FengShuiElement ObjectElement { get; set; }

    public FengShuiRelation Relation { get; set; }

    /// <summary>Điểm đóng góp (vd Tỷ hòa +1.0, Tương sinh +0.8, Bị khắc −1.0).</summary>
    public decimal Score { get; set; }

    /// <summary>Mô tả ngắn để AI dùng làm chất liệu giải thích (vd "Thủy sinh Mộc, nuôi dưỡng bản mệnh").</summary>
    public string? Description { get; set; }
}
