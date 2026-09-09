using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Phân tích ngũ hành của một workspace (không chạy cả phiên recommendation) — cho FE hiển thị
/// "phòng của bạn đang thiếu/thừa hành gì".
/// </summary>
public sealed record WorkspaceElementAnalysisResponse
{
    public Guid WorkspaceProfileId { get; init; }

    /// <summary>Hành có gap dương lớn nhất (thiếu nhiều nhất).</summary>
    public string DominantNeed { get; init; } = null!;

    /// <summary>Từng hành, sắp giảm dần theo Gap (thiếu nhất → thừa nhất).</summary>
    public List<ElementAnalysisRow> Elements { get; init; } = new();

    /// <summary>% phòng đúng chuẩn lý tưởng đã điều chỉnh theo mục đích + bản mệnh (0-100).</summary>
    public int CompatibilityPercent { get; init; }

    /// <summary>3 nhận định (trait/status/action) sinh ở BE theo case A/B/C.</summary>
    public SpaceInsights Insights { get; init; } = null!;

    // ===== Sản phẩm đã mua đặt vào phòng (tính lúc đọc, không lưu vector) =====

    /// <summary>true khi có sản phẩm CHƯA GIAO đặt trong phòng → FE vẽ thêm lớp radar preview (nét đứt).</summary>
    public bool HasPreview { get; init; }

    /// <summary>% tương thích của vector preview (gồm cả hàng đang giao). = CompatibilityPercent khi không có preview.</summary>
    public int PreviewCompatibilityPercent { get; init; }

    /// <summary>Danh sách sản phẩm đang đặt trong phòng (cả đã giao + đang giao).</summary>
    public List<PlacedProductResponse> PlacedProducts { get; init; } = new();

    // ===== Bằng chứng: Current được ghép từ những nguồn nào =====

    /// <summary>
    /// Từng nguồn tạo nên vector Current (nền phòng / tag user khai / sản phẩm đã đặt) kèm % đóng góp.
    /// FE dùng để hiển thị trên radar "tag nào chiếm bao nhiêu điểm" — sắp giảm dần theo SharePercent.
    /// </summary>
    public List<CurrentContributionRow> Contributions { get; init; } = new();

    /// <summary>Số bằng chứng THẬT (tag + sản phẩm). 0 = Current hoàn toàn suy ra từ nền loại phòng.</summary>
    public int EvidenceCount { get; init; }

    /// <summary>
    /// Tổng phiếu của mọi nguồn. Mẫu số của mọi <c>SharePercent</c> — có nó thì FE mô phỏng lại được
    /// "nếu chủ nhân nặng N phiếu thì phòng trông thế nào" mà không cần gọi lại API.
    /// </summary>
    public decimal TotalVotes { get; init; }

    /// <summary>
    /// Số mũ nén tương phản đã áp khi dựng <c>current</c> (<c>EVIDENCE_SATURATION_ALPHA</c>).
    /// <c>1</c> = tuyến tính.
    ///
    /// <para>
    /// FE cần con số này để <b>mô phỏng đổi số phiếu</b>: <c>current</c> là ảnh phi tuyến của khối
    /// lượng thô, nên phải nghịch đảo về khối lượng trước rồi mới đổi phiếu —
    /// <c>m[e] ∝ current[e]^(1/α)</c>, chuẩn lại theo <c>totalVotes</c>. Đảo tuyến tính thẳng trên
    /// <c>current</c> sẽ ra một căn phòng không tồn tại.
    /// </para>
    /// </summary>
    public decimal SaturationAlpha { get; init; }

    // ===== v3.2 §10.3 — trục cá nhân trên radar phòng =====

    /// <summary>
    /// "Sau khi tính bản mệnh của bạn, hệ thống đang ưu tiên bù hành nào" — lớp thứ tư của radar.
    /// <para>
    /// <b>Không phụ thuộc sản phẩm</b> (<c>ĝ</c> của phòng × <c>r</c> của bản mệnh) nên nó thuộc về
    /// màn hình phân tích phòng, không phải màn hình một sản phẩm. <c>null</c> khi phòng
    /// <c>Public</c>, khi user chưa có ngày sinh, hoặc khi tham số trục cá nhân đang tắt.
    /// </para>
    /// </summary>
    public PersonalDirectionResponse? PersonalDirection { get; init; }

    /// <summary>
    /// Tỉ lệ Current đến từ dữ liệu user khai thay vì nền phòng (0..1) — FE hiện badge độ tin cậy.
    /// = tổng phiếu của tag + sản phẩm / tổng phiếu.
    /// </summary>
    public decimal Confidence { get; init; }
}

/// <summary>Một nguồn đóng góp vào Current, đã quy ra %.</summary>
public sealed record CurrentContributionRow
{
    /// <summary>"Interior" | "Tag" | "Product".</summary>
    public string Source { get; init; } = null!;

    /// <summary>Nhãn tiếng Việt hiển thị cho user (tag: LabelVi; sản phẩm: tên sản phẩm).</summary>
    public string Label { get; init; } = null!;

    /// <summary>% nguồn này chiếm trong toàn bộ vector Current (tổng mọi nguồn = 100).</summary>
    public decimal SharePercent { get; init; }

    /// <summary>
    /// Số PHIẾU của nguồn — đơn vị gốc của mô hình, <c>SharePercent = Votes / TotalVotes</c>.
    /// FE hiện "3 phiếu" thay vì "23%": phiếu là con số ổn định, còn % thì đổi mỗi lần khai thêm tag.
    /// </summary>
    public decimal Votes { get; init; }

    /// <summary>Phân bổ phần trăm đó theo từng hành (tổng = SharePercent).</summary>
    public List<ContributionElementShare> Elements { get; init; } = new();

    /// <summary>Có khi Source = "Tag" — để FE highlight đúng chip tag.</summary>
    public string? InputKind { get; init; }
    public string? InputCode { get; init; }

    /// <summary>Có khi Source = "Product".</summary>
    public Guid? ProductId { get; init; }
}

public sealed record ContributionElementShare(string Element, decimal Percent);

public sealed record ElementAnalysisRow
{
    /// <summary>Kim / Moc / Thuy / Hoa / Tho.</summary>
    public string Element { get; init; } = null!;
    public decimal Ideal { get; init; }
    public decimal AdjustedIdeal { get; init; }
    public decimal Current { get; init; }

    /// <summary>AdjustedIdeal − Current: + thiếu, − thừa.</summary>
    public decimal Gap { get; init; }

    /// <summary>Current NẾU tính cả sản phẩm chưa giao tới (= Current khi không có preview).</summary>
    public decimal PreviewCurrent { get; init; }

    /// <summary>AdjustedIdeal − PreviewCurrent.</summary>
    public decimal PreviewGap { get; init; }
}

/// <summary>Case: "Imbalanced" (A) | "Balanced" (B) | "Toxic" (C).</summary>
public sealed record SpaceInsights(string Case, IReadOnlyList<SpaceInsightLine> Lines);

/// <summary>
/// Kind: "trait" (đặc tính loại phòng) | "status" (hiện trạng + nguyên do) | "action" (đề xuất).
/// FE map icon theo Kind, Title đến từ BE.
/// </summary>
public sealed record SpaceInsightLine(string Kind, string Title, string Text);

/// <summary>
/// Trục cá nhân của MỘT CĂN PHÒNG (chưa gắn với sản phẩm nào) — v3.2 §10.3.
///
/// <para>
/// Có đủ <see cref="NormalizedGap"/> (ĝ), <see cref="RuleScore"/> (r) và
/// <see cref="PersonalWeight"/> nên <b>FE tự dựng lại <c>d</c> và <c>priorityVector</c> ở mọi mức
/// <c>Wp</c></b> — slider mô phỏng trọng số không cần gọi lại API.
/// </para>
/// </summary>
public sealed record PersonalDirectionResponse
{
    /// <summary>Trọng số bản mệnh đang áp cho phòng này (<c>Wp</c>).</summary>
    public decimal PersonalWeight { get; init; }

    /// <summary><c>PERSONAL_WEIGHT_PRIVATE</c> | <c>_SHARED</c> | <c>_PUBLIC</c>.</summary>
    public string PersonalWeightCode { get; init; } = null!;

    public string Scope { get; init; } = null!;

    /// <summary>Vì sao trọng số là con số đó — hiện trong tooltip của chip.</summary>
    public string ReasonVi { get; init; } = null!;

    /// <summary>Hành Nạp Âm của user, vd <c>"Moc"</c>.</summary>
    public string DestinyElement { get; init; } = null!;

    /// <summary>vd <c>"Moc — Đại Lâm Mộc (1988)"</c>.</summary>
    public string DestinyLabelVi { get; init; } = null!;

    /// <summary><c>ĝ = gap / (|gap|₁/2)</c> — mỗi trục ∈ [−1,+1].</summary>
    public List<ProductElementRow> NormalizedGap { get; init; } = new();

    /// <summary><c>r[e] = ruleScore(bản mệnh, e)</c> — CÓ DẤU, từ bảng <c>feng_shui_rules</c>.</summary>
    public List<ProductElementRow> RuleScore { get; init; } = new();

    /// <summary><c>d = (1−Wp)·ĝ + Wp·r</c>.</summary>
    public List<ProductElementRow> CombinedDirection { get; init; } = new();

    /// <summary>
    /// Vector bản mệnh Σ=1: bản mệnh 60% · hành sinh ra mệnh 30% · hành mệnh sinh ra 10%
    /// (tỉ lệ lấy từ <c>SELF/SUPPORT/CHILD_SHARE</c>).
    /// </summary>
    public List<ProductElementRow> PersonalVector { get; init; } = new();

    /// <summary>
    /// <c>normalize(max(d, 0))</c>, Σ=1 — "hệ thống đang ưu tiên BÙ hành nào". Nó chỉ trải trên các
    /// trục còn dương nên luôn nhọn hơn <c>adjustedIdeal</c>, KHÔNG so sánh trực tiếp được với nó.
    /// Để dành cho radar phụ (§10.5), không vẽ chồng lên radar phòng.
    /// </summary>
    public List<ProductElementRow> PriorityVector { get; init; } = new();

    /// <summary>§13 — phòng thiếu đúng hành khắc bản mệnh. <c>null</c> khi không có xung khắc.</summary>
    public ConflictResolutionResponse? ConflictResolution { get; init; }
}
