using FengDeskAI.Domain.Entities.Recommendation;

using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Kết quả phân tích ngũ hành của một workspace: 4 vector chuẩn hóa (Σ=1) trừ <see cref="Gap"/>.
/// <list type="bullet">
/// <item><see cref="Ideal"/> — vector lý tưởng theo loại phòng.</item>
/// <item><see cref="AdjustedIdeal"/> — ideal đã bẻ theo intent (mục đích làm việc).</item>
/// <item><see cref="Current"/> — hiện trạng phòng (màu/vật liệu khai báo, hoặc fallback interior).</item>
/// <item><see cref="Gap"/> — AdjustedIdeal − Current (Σ=0; + thiếu, − thừa). KHÔNG chuẩn hóa.</item>
/// </list>
/// </summary>
public sealed record WorkspaceElementAnalysis(
    ElementVector Ideal,
    ElementVector AdjustedIdeal,
    ElementVector Current,
    ElementVector Gap);

/// <summary>
/// Thuần (không I/O): dựng 4 vector ngũ hành phòng từ dữ liệu đã nạp sẵn. Tách khỏi phần inline
/// trong <c>RecommendationService.GenerateAsync</c> để engine chấm điểm và endpoint element-analysis
/// dùng chung một công thức — bảo đảm Gap giống hệt nhau.
/// </summary>
public static class WorkspaceElementAnalyzer
{
    public static WorkspaceElementAnalysis Analyze(
        IReadOnlyCollection<WorkspaceTypeElement> typeElements,
        IEnumerable<WorkPurposeElementModifier> modifiers,
        IReadOnlyCollection<WorkspaceProfileInput> profileInputs,
        ElementInputResolver resolver,
        PersonPresence? person = null,
        decimal? interiorVotes = null,
        decimal? saturationAlpha = null,
        IReadOnlyCollection<ProductContribution>? placedProducts = null,
        ScoringParameters? budgetParams = null,
        WorkspaceScope? scope = null)
    {
        var ideal = WorkspaceVectorBuilder.BuildIdeal(typeElements);
        var adjustedIdeal = WorkspaceVectorBuilder.ApplyIntent(ideal, modifiers);
        // Chủ nhân phòng vào LUÔN current dùng để chấm điểm, không chỉ để vẽ radar — nếu chỉ vẽ thì
        // hình và điểm nói hai chuyện khác nhau về cùng một căn phòng.
        // Sản phẩm ĐÃ GIAO là hiện trạng thật của phòng, phải có mặt ở đây chứ không chỉ trên radar:
        // để trống thì bộ gợi ý tiếp tục đẩy đúng hành mà user vừa mua đồ về bù (xem PlacedProductBuilder).
        var current = WorkspaceVectorBuilder.BuildCurrentBreakdown(
                profileInputs, resolver, typeElements,
                placedProducts ?? Array.Empty<ProductContribution>(),
                person, interiorVotes, saturationAlpha, budgetParams, scope)
            .Current;
        var gap = adjustedIdeal.Subtract(current);
        return new WorkspaceElementAnalysis(ideal, adjustedIdeal, current, gap);
    }
}
