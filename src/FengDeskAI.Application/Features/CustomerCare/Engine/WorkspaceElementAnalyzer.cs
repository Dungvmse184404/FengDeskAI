using FengDeskAI.Domain.Entities.Recommendation;

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
        ElementInputResolver resolver)
    {
        var ideal = WorkspaceVectorBuilder.BuildIdeal(typeElements);
        var adjustedIdeal = WorkspaceVectorBuilder.ApplyIntent(ideal, modifiers);
        var current = WorkspaceVectorBuilder.BuildCurrent(profileInputs, resolver, typeElements);
        var gap = adjustedIdeal.Subtract(current);
        return new WorkspaceElementAnalysis(ideal, adjustedIdeal, current, gap);
    }
}
