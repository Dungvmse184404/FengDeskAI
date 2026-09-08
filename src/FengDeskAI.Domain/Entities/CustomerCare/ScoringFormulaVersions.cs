namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Phiên bản công thức chấm điểm đã đóng dấu lên từng phiên gợi ý
/// (<see cref="Recommendation.FormulaVersion"/>).
/// <para>
/// Không dùng enum: giá trị này đi thẳng ra JSON và xuống DB dưới dạng chuỗi, và danh sách chỉ dài
/// thêm theo thời gian chứ không bao giờ bị suy diễn logic — cùng lý do <c>scoring_params.code</c> là
/// chuỗi chứ không phải enum.
/// </para>
/// </summary>
public static class ScoringFormulaVersions
{
    /// <summary><c>gapScore = gap·p / |gap|₁</c> — miền ±0.5; penalty 0.30/0.15/0.20/0.05.</summary>
    public const string V31 = "3.1";

    /// <summary>
    /// <c>gapScore = gap·p / (|gap|₁/2)</c> — miền ±1.0; penalty ×2;
    /// <c>PersonalConflictMode.Scaled</c> (L2). Xem <c>docs/adr/score-explainability-v3.2.md</c>.
    /// </summary>
    public const string V32 = "3.2";

    /// <summary>Phiên bản mà engine đang chạy — đóng dấu lên mọi phiên gợi ý mới.</summary>
    public const string Current = V32;
}
