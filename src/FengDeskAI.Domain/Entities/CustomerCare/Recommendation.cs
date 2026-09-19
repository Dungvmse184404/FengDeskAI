using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Domain.Entities.CustomerCare;

/// <summary>
/// Một phiên gợi ý sản phẩm cho khách trên một workspace cụ thể. Lưu snapshot các yếu tố
/// đầu vào đã tính (mệnh, Kua, trọng số) để tái hiện và audit lại kết quả.
/// </summary>
public class Recommendation : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>
    /// Hồ sơ không gian được chấm. NULL khi <see cref="Kind"/> = <see cref="RecommendationKind.PersonalCarry"/>
    /// (vật phẩm mang theo người thì không gắn với phòng nào).
    /// </summary>
    public Guid? WorkspaceProfileId { get; set; }

    /// <summary>Loại phiên — FE/thống kê dựa vào đây để biết phiên có workspace hay không.</summary>
    public RecommendationKind Kind { get; set; } = RecommendationKind.Workspace;

    /// <summary>Snapshot loại không gian tại thời điểm gợi ý (loại có thể bị sửa sau).</summary>
    public Guid? WorkspaceTypeId { get; set; }

    /// <summary>
    /// Mệnh ngũ hành của khách (Nạp Âm). Null khi giới tính không phải Nam/Nữ →
    /// toàn bộ phần điểm cá nhân bị bỏ qua, chỉ chấm theo yếu tố chức năng.
    /// </summary>
    public FengShuiElement? CustomerElement { get; set; }

    /// <summary>Kua number của khách (1..9, trừ 5). Null khi không tính được (giới tính không xác định).</summary>
    public int? KuaNumber { get; set; }

    public KuaGroup? KuaGroup { get; set; }

    /// <summary>Trọng số cá nhân đã áp dụng (từ WorkspaceType): 1.0 riêng tư, 0.5 công cộng.</summary>
    public decimal PersonalWeight { get; set; }

    /// <summary>
    /// Phiên bản CÔNG THỨC đã sinh ra <c>RecommendationItem.Score</c> của phiên này —
    /// <c>"3.1"</c> (gapScore chia <c>|gap|₁</c>, miền ±0.5) hoặc <c>"3.2"</c> (chia <c>|gap|₁/2</c>,
    /// miền ±1.0 + penalty ×2 + <c>PersonalConflictMode.Scaled</c>).
    /// <para>
    /// Điểm của hai phiên bản KHÔNG so sánh được với nhau: cùng một sản phẩm × cùng một phòng cho hai
    /// con số khác hẳn. FE phải ẩn mọi so sánh chéo phiên bản. Row cũ (sinh trước v3.2) mang
    /// <c>"3.1"</c> nhờ default của cột.
    /// </para>
    /// Xem <c>docs/adr/score-explainability-v3.2.md</c> §8.4.
    /// </summary>
    public string FormulaVersion { get; set; } = ScoringFormulaVersions.Current;

    public RecommendationStatus Status { get; set; } = RecommendationStatus.Scored;

    /// <summary>Tổng kết do AI sinh cho cả phiên (optional).</summary>
    public string? Summary { get; set; }

    public User User { get; set; } = null!;
    public WorkspaceProfile? WorkspaceProfile { get; set; }
    public ICollection<RecommendationItem> Items { get; set; } = new List<RecommendationItem>();
    public ICollection<RecommendationLog> Logs { get; set; } = new List<RecommendationLog>();
}
