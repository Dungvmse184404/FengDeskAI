using FengDeskAI.Application.Features.CustomerCare.Engine;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs;

/// <summary>
/// Giải thích con số hiển thị trên màn hình — v3.2 §9.2.
///
/// <para>
/// <b>Nguyên tắc:</b> <c>Code</c> để FE map i18n/icon (đừng parse nhãn); <c>LabelVi</c>/<c>ReasonVi</c>
/// LUÔN đi kèm số, vì một con số trần không giải thích được gì.
/// </para>
/// <para>
/// <b>Không gửi khối này cho LLM.</b> Model đọc số thô sẽ bịa ra phép tính của riêng nó và nói sai;
/// lớp AI vẫn chỉ nhận <c>matchFacts</c>/<c>cautionFacts</c> đã thành câu.
/// </para>
/// </summary>
public sealed record ScoreBreakdownResponse
{
    /// <summary>
    /// <c>"3.1"</c> hoặc <c>"3.2"</c>. Điểm của hai phiên bản KHÔNG so sánh được với nhau — FE phải ẩn
    /// mọi so sánh chéo phiên bản (§8.4).
    /// </summary>
    public string FormulaVersion { get; init; } = null!;

    /// <summary><c>WorkspaceGap</c> (đồ đặt trong phòng) | <c>PersonalNeed</c> (vật mang theo người).</summary>
    public string Target { get; init; } = null!;

    public string Placement { get; init; } = null!;

    /// <summary>Điểm quy ra thang 0..100 đúng công thức FE dùng: <c>(score + 1) / 2 × 100</c>.</summary>
    public int DisplayPercent { get; init; }

    /// <summary>Các số hạng CỘNG. Σ <c>Contribution</c> = <c>Blended</c>.</summary>
    public List<ScoreComponentRow> Components { get; init; } = new();

    /// <summary>Các số hạng TRỪ — liệt kê đủ loại kể cả <c>Applied = false</c>.</summary>
    public List<ScorePenaltyRow> Penalties { get; init; } = new();

    /// <summary>Tổng phần cộng, trước khi trừ penalty.</summary>
    public decimal Blended { get; init; }

    /// <summary>Điểm trước khi clamp về [−1,1] và làm tròn.</summary>
    public decimal RawScore { get; init; }

    /// <summary><c>true</c> khi <see cref="RawScore"/> đã bị cắt về biên.</summary>
    public bool Clamped { get; init; }

    /// <summary>Điểm cuối, khớp <c>ProductFitResponse.Score</c>.</summary>
    public decimal Score { get; init; }

    public PersonalWeightInfo? PersonalWeight { get; init; }

    public ScoreVectorsResponse Vectors { get; init; } = new();

    /// <summary>Hành Nạp Âm của user — <c>null</c> khi chưa có ngày sinh.</summary>
    public string? DestinyElement { get; init; }

    /// <summary>vd <c>"Mộc — Đại Lâm Mộc (1988)"</c>. <c>null</c> khi chưa có ngày sinh.</summary>
    public string? DestinyLabelVi { get; init; }

    /// <summary>
    /// Nghề nghiệp đã tác động vào điểm này (P5). <c>null</c> khi user chưa khai nghề, nghề chưa có
    /// delta, hoặc <c>OCCUPATION_SHARE</c> đang tắt — cả ba đều nghĩa là nghề nghiệp không đổi gì.
    /// </summary>
    public OccupationInfluenceResponse? Occupation { get; init; }

    /// <summary>
    /// §13 — phòng đang thiếu đúng hành khắc bản mệnh. <c>null</c> khi không có xung khắc.
    /// FE hiện banner cảnh báo nhẹ + highlight trục hành hoá giải trên radar.
    /// </summary>
    public ConflictResolutionResponse? ConflictResolution { get; init; }
}

/// <summary>Một số hạng cộng vào điểm: <c>Contribution = Value × Weight</c>.</summary>
public sealed record ScoreComponentRow
{
    /// <summary><c>GAP_SCORE</c> | <c>PERSONAL_SCORE</c> | <c>PERSONAL_NEED_SCORE</c>.</summary>
    public string Code { get; init; } = null!;

    public string LabelVi { get; init; } = null!;
    public decimal Value { get; init; }
    public decimal Weight { get; init; }
    public decimal Contribution { get; init; }
    public string ReasonVi { get; init; } = null!;
}

/// <summary>Một số hạng trừ khỏi điểm.</summary>
public sealed record ScorePenaltyRow
{
    /// <summary>Trùng <c>scoring_params.code</c>: <c>USER_CONFLICT_PENALTY</c>, <c>DIRECTION_PENALTY</c>, <c>VIBE_*</c>.</summary>
    public string Code { get; init; } = null!;

    public string LabelVi { get; init; } = null!;
    public decimal Value { get; init; }

    /// <summary><c>false</c> = đã xét nhưng không trừ. Khác hẳn "không tồn tại" — FE vẫn liệt kê.</summary>
    public bool Applied { get; init; }

    public string ReasonVi { get; init; } = null!;
}

/// <summary>Trọng số trục cá nhân đã áp cho phiên chấm này.</summary>
public sealed record PersonalWeightInfo
{
    public decimal Value { get; init; }

    /// <summary><c>PERSONAL_WEIGHT_PRIVATE</c> | <c>_SHARED</c> | <c>_PUBLIC</c>.</summary>
    public string Code { get; init; } = null!;

    public string Scope { get; init; } = null!;
    public string ReasonVi { get; init; } = null!;
}

/// <summary>
/// Các vector để FE vẽ radar. <b>Có đủ <c>ĝ</c>, <c>r</c> và <c>Wp</c> nên FE tự dựng lại
/// <c>d</c> và <c>priorityVector</c> ở mọi mức <c>Wp</c> — slider mô phỏng không cần gọi lại API</b> (§10.3).
/// </summary>
public sealed record ScoreVectorsResponse
{
    /// <summary>Vector ngũ hành của sản phẩm, Σ=1.</summary>
    public List<ProductElementRow> Product { get; init; } = new();

    /// <summary>
    /// <c>ĝ = gap / (|gap|₁/2)</c>, mỗi trục ∈ [−1,+1]. Ở nhánh <c>PersonalNeed</c> đây là vector dụng
    /// thần đã chuẩn hoá (Σ=1, không âm), không phải gap.
    /// </summary>
    public List<ProductElementRow> NormalizedGap { get; init; } = new();

    /// <summary>
    /// <c>r'[e]</c> — điểm quan hệ ĐÃ tính nghề nghiệp, CÓ DẤU. <c>null</c> khi trục cá nhân tắt.
    /// Bằng <see cref="BaseRuleScore"/> khi nghề nghiệp không áp.
    /// </summary>
    public List<ProductElementRow>? RuleScore { get; init; }

    /// <summary>
    /// <c>r</c> TRƯỚC khi cộng delta nghề nghiệp. <c>null</c> khi nghề nghiệp không áp — khi đó
    /// <see cref="RuleScore"/> đã là <c>r</c> gốc.
    /// </summary>
    public List<ProductElementRow>? BaseRuleScore { get; init; }

    /// <summary>
    /// <c>r' − r</c> — <b>lớp radar nghề nghiệp</b>: nghề của bạn đã kéo hành nào lên/xuống bao nhiêu.
    /// Là mức dịch THẬT SAU khi chặn, nên hành khắc mệnh hiện ra gần 0 dù delta khai lớn — đúng thứ
    /// cần cho user thấy. <c>null</c> khi nghề nghiệp không áp.
    /// </summary>
    public List<ProductElementRow>? OccupationShift { get; init; }

    /// <summary><c>d = (1−Wp)·ĝ + Wp·r'</c> — thứ thật sự nhân với vector sản phẩm.</summary>
    public List<ProductElementRow> CombinedDirection { get; init; } = new();

    /// <summary>
    /// <c>normalize(max(d, 0))</c>, Σ=1 — <b>lớp vàng trên radar</b>. Chồng thẳng lên
    /// <c>adjustedIdeal</c>/<c>current</c> vì cùng thang. Trục nào <c>d &lt; 0</c> thì ở đây bằng 0 và
    /// FE tô nhãn trục đỏ.
    /// </summary>
    public List<ProductElementRow> PriorityVector { get; init; } = new();

    /// <summary>Vector dụng thần — chỉ luồng <c>Carry</c>, <c>null</c> ở luồng phòng.</summary>
    public List<ProductElementRow>? PersonalNeed { get; init; }

    /// <summary>Vector bản mệnh Σ=1 (60/30/10). <c>null</c> khi user chưa có ngày sinh.</summary>
    public List<ProductElementRow>? PersonalVector { get; init; }

    /// <summary>
    /// <c>T = (1−Wp)·adjustedIdeal + Wp·personalVector</c>, Σ=1 — <b>lớp radar "Mục tiêu của bạn"</b>.
    /// Nằm gọn cùng thang với <c>adjustedIdeal</c>/<c>current</c> vì tổ hợp lồi của hai vector Σ=1.
    /// <para>⚠️ CHỈ để hiển thị — điểm số vẫn đi đường <see cref="CombinedDirection"/> (§10.2).</para>
    /// </summary>
    public List<ProductElementRow>? PersonalTarget { get; init; }
}

/// <summary>Nghề nghiệp đã bẻ vector điểm quan hệ thế nào — v3.2 §11 (P5).</summary>
public sealed record OccupationInfluenceResponse
{
    /// <summary>Mã bất biến, vd <c>"IT"</c>.</summary>
    public string Code { get; init; } = null!;

    /// <summary>Tên hiển thị, vd <c>"CNTT / Lập trình"</c>.</summary>
    public string NameVi { get; init; } = null!;

    /// <summary><c>OCCUPATION_SHARE</c> đang áp.</summary>
    public decimal Share { get; init; }

    /// <summary>Mã dòng <c>scoring_params</c> quyết định <see cref="Share"/>.</summary>
    public string ShareCode { get; init; } = ScoringParamCodes.OccupationShare;

    /// <summary>Câu giải thích cho tooltip — nói cả phần nghề nghiệp KHÔNG kéo được.</summary>
    public string ReasonVi { get; init; } = null!;
}

/// <summary>§13 — hành hoá giải khi phòng cần đúng hành khắc bản mệnh.</summary>
public sealed record ConflictResolutionResponse
{
    /// <summary>Hành phòng đang thiếu nhất.</summary>
    public string RoomNeed { get; init; } = null!;

    /// <summary>Bản mệnh user.</summary>
    public string Destiny { get; init; } = null!;

    /// <summary>Hành trung gian: <c>RoomNeed</c> sinh nó, nó sinh <c>Destiny</c>.</summary>
    public string Bridge { get; init; } = null!;

    public string ReasonVi { get; init; } = null!;
}
