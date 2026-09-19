namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Engine chấm điểm phong thủy deterministic (v3 — mô hình vector ngũ hành). Thuần logic —
/// không chạm DB. Orchestrator dựng vector (người/phòng/sản phẩm) rồi gọi engine; AI chỉ diễn giải.
/// </summary>
public interface IRecommendationScorer
{
    /// <summary>Chấm điểm ứng viên theo Gap + lọc mệnh + Directional Validation, trả sắp xếp giảm dần.</summary>
    IReadOnlyList<ScoredProduct> Score(ScoringContext context, IReadOnlyList<ProductFacts> candidates);

    /// <summary>
    /// Chấm điểm 1 sản phẩm × 1 phòng — KHÔNG BAO GIỜ loại bỏ (khác <see cref="Score"/>): xung mệnh
    /// hay lệch vibe chỉ phản ánh vào điểm/caution, dùng cho trang chi tiết sản phẩm.
    /// </summary>
    ScoredProduct ScoreSingle(ScoringContext context, ProductFacts product);

    /// <summary>
    /// Chấm điểm 1 sản phẩm theo BẢN MỆNH người dùng (nhánh <see cref="ScoringTarget.PersonalNeed"/>) —
    /// v3.2 §10.6 · R3. Cũng không bao giờ loại bỏ, như <see cref="ScoreSingle"/>.
    /// <para>
    /// Khác <see cref="ScoreSingle"/> ở chỗ mục tiêu là <see cref="ScoringContext.PersonalNeedVector"/>
    /// chứ không phải gap của phòng: vật mang theo người đi cùng chủ nhân, không thuộc phòng nào.
    /// </para>
    /// </summary>
    ScoredProduct ScoreSinglePersonal(ScoringContext context, ProductFacts product);
}
