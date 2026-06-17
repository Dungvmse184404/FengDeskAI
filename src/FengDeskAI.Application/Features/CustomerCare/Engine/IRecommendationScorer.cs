using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Engine chấm điểm phong thủy deterministic. Thuần logic — không chạm DB. Orchestrator nạp
/// dữ liệu (rules, sản phẩm) rồi gọi engine; AI service chỉ diễn giải dựa trên kết quả này.
/// </summary>
public interface IRecommendationScorer
{
    /// <summary>
    /// Xây hồ sơ cá nhân từ ngày sinh + giới tính. Trả null khi giới tính không phải Nam/Nữ
    /// (theo quy tắc: bỏ qua phần cá nhân) hoặc thiếu ngày sinh.
    /// </summary>
    PersonalProfile? BuildPersonalProfile(DateTime? dateOfBirth, Gender gender);

    /// <summary>Chấm điểm danh sách ứng viên, trả về đã sắp xếp giảm dần theo điểm.</summary>
    IReadOnlyList<ScoredProduct> Score(ScoringContext context, IReadOnlyList<ProductFacts> candidates);
}
