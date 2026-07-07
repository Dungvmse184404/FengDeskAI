using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Recommendation;

/// <summary>
/// Tham số phẳng cho engine chấm điểm v3 (thay <c>ScoringWeightProfile</c> cũ). Admin chỉnh;
/// engine dùng default trong code khi thiếu row. Xem hằng <c>ScoringParamCodes</c>.
/// </summary>
public class ScoringParam : BaseEntity
{
    public string Code { get; set; } = null!;
    public decimal Value { get; set; }
    public string? Description { get; set; }
}
