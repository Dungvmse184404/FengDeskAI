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
}
