using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Mã tham số engine v3 (khớp cột <c>scoring_params.code</c>). Xem PHẦN F của spec.</summary>
public static class ScoringParamCodes
{
    public const string SelfShare = "SELF_SHARE";
    public const string SupportShare = "SUPPORT_SHARE";
    public const string ChildShare = "CHILD_SHARE";
    public const string MaterialShare = "MATERIAL_SHARE";
    public const string ColorShare = "COLOR_SHARE";
    public const string UserConflictPenalty = "USER_CONFLICT_PENALTY";
    public const string DirectionPenalty = "DIRECTION_PENALTY";
    public const string FallbackPrimary = "FALLBACK_PRIMARY";
    public const string FallbackSecondary = "FALLBACK_SECONDARY";
    public const string CarryPrimaryShare = "CARRY_PRIMARY_SHARE";
    public const string CarrySecondaryShare = "CARRY_SECONDARY_SHARE";
    public const string VibeMismatchPenalty = "VIBE_MISMATCH_PENALTY";
    public const string VibeUnknownPenalty = "VIBE_UNKNOWN_PENALTY";
    public const string VibeFilterHard = "VIBE_FILTER_HARD";
    public const string MinScoreThreshold = "MIN_SCORE_THRESHOLD";

    // v3.1 — trọng số trục cá nhân theo WorkspaceScope (xem personalized-recommendation-v3.1.md §3.2).
    public const string PersonalWeightPrivate = "PERSONAL_WEIGHT_PRIVATE";
    public const string PersonalWeightShared = "PERSONAL_WEIGHT_SHARED";
    public const string PersonalWeightPublic = "PERSONAL_WEIGHT_PUBLIC";
}

/// <summary>
/// Bộ tham số phẳng của engine (nạp từ <c>scoring_params</c>, thiếu row → default trong code).
/// </summary>
public sealed record ScoringParameters
{
    public decimal SelfShare { get; init; } = 0.60m;
    public decimal SupportShare { get; init; } = 0.30m;
    public decimal ChildShare { get; init; } = 0.10m;
    public decimal MaterialShare { get; init; } = 0.60m;
    public decimal ColorShare { get; init; } = 0.40m;
    public decimal UserConflictPenalty { get; init; } = 0.30m;
    public decimal DirectionPenalty { get; init; } = 0.15m;
    public decimal FallbackPrimary { get; init; } = 0.70m;
    public decimal FallbackSecondary { get; init; } = 0.30m;

    /// <summary>Trọng số dụng thần CHÍNH khi dựng vector mục tiêu cho vật phẩm mang theo người.</summary>
    public decimal CarryPrimaryShare { get; init; } = 0.60m;

    /// <summary>Trọng số dụng thần PHỤ (và các hành sau) cho vật phẩm mang theo người.</summary>
    public decimal CarrySecondaryShare { get; init; } = 0.40m;

    /// <summary>Trừ điểm khi sản phẩm CÓ vibe nhưng không chứa vibe hợp mục đích phòng.</summary>
    public decimal VibeMismatchPenalty { get; init; } = 0.20m;

    /// <summary>
    /// Trừ điểm khi sản phẩm CHƯA khai vibe nào. Nhẹ hơn <see cref="VibeMismatchPenalty"/>:
    /// "chưa biết" là thiếu dữ liệu, không phải bằng chứng lệch mục đích.
    /// </summary>
    public decimal VibeUnknownPenalty { get; init; } = 0.05m;

    /// <summary>
    /// Kill-switch: ≥ 0.5 → giữ nguyên hành vi v3 (lệch vibe bị LOẠI khỏi candidates);
    /// &lt; 0.5 → chuyển sang trừ điểm mềm. Seed 1.0 để merge không đổi ranking; hạ về 0
    /// qua API scoring-config sau khi đã đối chiếu golden set.
    /// </summary>
    public decimal VibeFilterHard { get; init; } = 1.00m;

    /// <summary>
    /// Điểm cuối dưới ngưỡng này thì loại khỏi danh sách gợi ý (chỉ mode Rank). Mặc định −1.0 =
    /// KHÔNG cắt (điểm đã clamp về [−1,1]). Đây là lưới an toàn thay cho các bộ lọc theo thuộc tính
    /// đơn lẻ — nâng lên 0.0 khi tắt <see cref="VibeFilterHard"/>.
    /// </summary>
    public decimal MinScoreThreshold { get; init; } = -1.00m;

    /// <summary>
    /// Tỉ trọng <c>personalScore</c> trong điểm cuối ở không gian <see cref="WorkspaceScope.Private"/>
    /// (phần còn lại <c>1 − Wp</c> dành cho gap phòng). <b>Seed 0</b> = kill-switch: giữ nguyên hành vi
    /// trước v3.1 (hard-filter khắc mệnh + <see cref="UserConflictPenalty"/>). Giá trị đích 0.50.
    /// </summary>
    public decimal PersonalWeightPrivate { get; init; } = 0.50m;

    /// <summary>Như trên, cho <see cref="WorkspaceScope.Shared"/> (phòng khách, bếp, phòng họp). Đích 0.30.</summary>
    public decimal PersonalWeightShared { get; init; } = 0.30m;

    /// <summary>
    /// Như trên, cho <see cref="WorkspaceScope.Public"/> (lễ tân, khu mở). Luôn 0 — không gian chung
    /// không được neo vào bản mệnh của một người.
    /// </summary>
    public decimal PersonalWeightPublic { get; init; } = 0.00m;

    /// <summary>Tỉ trọng cá nhân ứng với một scope. Dùng ở <c>RecommendationService.ResolvePersonalWeight</c>.</summary>
    public decimal PersonalWeightFor(WorkspaceScope scope) => scope switch
    {
        WorkspaceScope.Private => PersonalWeightPrivate,
        WorkspaceScope.Shared => PersonalWeightShared,
        WorkspaceScope.Public => PersonalWeightPublic,
        _ => PersonalWeightPrivate,
    };

    public static ScoringParameters Default { get; } = new();

    /// <summary>Dựng từ các row DB; code lạ bị bỏ qua, code thiếu giữ default.</summary>
    public static ScoringParameters FromRows(IEnumerable<ScoringParam> rows)
    {
        var map = rows.ToDictionary(r => r.Code, r => r.Value, StringComparer.OrdinalIgnoreCase);
        var d = Default;
        decimal V(string code, decimal fallback) => map.TryGetValue(code, out var v) ? v : fallback;
        return new ScoringParameters
        {
            SelfShare = V(ScoringParamCodes.SelfShare, d.SelfShare),
            SupportShare = V(ScoringParamCodes.SupportShare, d.SupportShare),
            ChildShare = V(ScoringParamCodes.ChildShare, d.ChildShare),
            MaterialShare = V(ScoringParamCodes.MaterialShare, d.MaterialShare),
            ColorShare = V(ScoringParamCodes.ColorShare, d.ColorShare),
            UserConflictPenalty = V(ScoringParamCodes.UserConflictPenalty, d.UserConflictPenalty),
            DirectionPenalty = V(ScoringParamCodes.DirectionPenalty, d.DirectionPenalty),
            FallbackPrimary = V(ScoringParamCodes.FallbackPrimary, d.FallbackPrimary),
            FallbackSecondary = V(ScoringParamCodes.FallbackSecondary, d.FallbackSecondary),
            CarryPrimaryShare = V(ScoringParamCodes.CarryPrimaryShare, d.CarryPrimaryShare),
            CarrySecondaryShare = V(ScoringParamCodes.CarrySecondaryShare, d.CarrySecondaryShare),
            VibeMismatchPenalty = V(ScoringParamCodes.VibeMismatchPenalty, d.VibeMismatchPenalty),
            VibeUnknownPenalty = V(ScoringParamCodes.VibeUnknownPenalty, d.VibeUnknownPenalty),
            VibeFilterHard = V(ScoringParamCodes.VibeFilterHard, d.VibeFilterHard),
            MinScoreThreshold = V(ScoringParamCodes.MinScoreThreshold, d.MinScoreThreshold),
            PersonalWeightPrivate = V(ScoringParamCodes.PersonalWeightPrivate, d.PersonalWeightPrivate),
            PersonalWeightShared = V(ScoringParamCodes.PersonalWeightShared, d.PersonalWeightShared),
            PersonalWeightPublic = V(ScoringParamCodes.PersonalWeightPublic, d.PersonalWeightPublic),
        };
    }
}

/// <summary>
/// Hồ sơ phong thủy cá nhân (mệnh Nạp Âm + Kua) — GIỮ cho hiển thị hồ sơ &amp; AI diễn giải.
/// Điểm v3 không dùng Kua; chỉ mệnh (qua <see cref="Element"/>) tham gia bộ lọc.
/// </summary>
public sealed record PersonalProfile(
    FengShuiElement Element,
    int? KuaNumber,
    KuaGroup? Group,
    IReadOnlySet<CompassDirection> FavorableDirections);

/// <summary>
/// Bối cảnh 1 phiên chấm điểm v3. Mọi thực thể quy về <see cref="ElementVector"/>.
/// </summary>
public sealed record ScoringContext
{
    /// <summary>Null → bỏ qua bộ lọc mệnh (thiếu ngày sinh).</summary>
    public ElementVector? PersonalVector { get; init; }

    /// <summary>Vector lý tưởng đã bẻ theo Intent.</summary>
    public required ElementVector AdjustedIdeal { get; init; }

    /// <summary>Vector hiện trạng phòng (màu/vật liệu, hoặc Interior mặc định).</summary>
    public required ElementVector CurrentVector { get; init; }

    /// <summary>
    /// Vector mục tiêu của NGƯỜI (dụng thần Tứ Trụ, fallback Nạp Âm) — chỉ dùng cho
    /// <see cref="ProductPlacement.Carry"/>. Null ở luồng workspace.
    /// </summary>
    public ElementVector? PersonalNeedVector { get; init; }

    public required WorkspaceScope Scope { get; init; }
    public required WorkPurpose Purpose { get; init; }

    /// <summary>Hướng bị chắn (cửa vào ∪ WC ∪ góc tối) — dùng ở Directional Validation.</summary>
    public IReadOnlySet<CompassDirection> ViolatedDirections { get; init; } = new HashSet<CompassDirection>();

    /// <summary>
    /// Tỉ trọng <c>personalScore</c> trong điểm cuối của nhánh <see cref="ScoringTarget.WorkspaceGap"/>
    /// (v3.1). <c>0</c> = tắt trục cá nhân → giữ nguyên hành vi trước v3.1 (hard-filter khắc mệnh +
    /// <see cref="ScoringParameters.UserConflictPenalty"/>). Không áp cho nhánh
    /// <see cref="ScoringTarget.PersonalNeed"/> — nhánh đó vốn đã 100% cá nhân.
    /// </summary>
    public decimal PersonalWeight { get; init; }

    /// <summary>
    /// Bảng điểm quan hệ ngũ hành nạp từ <c>feng_shui_rules</c> (admin chỉnh được): khóa
    /// <c>(mệnh user, hành sản phẩm)</c>. Null/thiếu khóa → rơi về
    /// <see cref="FengShuiCalculator.DefaultScore"/>.
    /// </summary>
    public IReadOnlyDictionary<(FengShuiElement Subject, FengShuiElement Object), decimal>? RuleScores { get; init; }

    /// <summary>Mục tiêu người dùng nêu trong phiên này (đã dùng để lọc ứng viên). Null = không nêu.</summary>
    public Aspiration? Aspiration { get; init; }

    /// <summary>
    /// Hướng Bát Trạch ưu tiên theo <see cref="Aspiration"/>, tốt nhất trước. Rỗng → <c>placementHint</c>
    /// giữ hành vi cũ (lấy hướng hợp đầu tiên).
    /// </summary>
    public IReadOnlyList<AspirationDirection> AspirationDirections { get; init; } = Array.Empty<AspirationDirection>();

    public ScoringParameters Params { get; init; } = ScoringParameters.Default;

    /// <summary>Trục cá nhân có đang bật không: cần cả trọng số &gt; 0 lẫn vector mệnh (user có ngày sinh).</summary>
    public bool PersonalBlendActive => PersonalWeight > 0m && PersonalVector is not null;

    /// <summary>
    /// Điểm quan hệ từ mệnh user tới một hành, có dấu: tỷ hòa +1.0 … bị khắc −1.0.
    /// Ưu tiên giá trị admin cấu hình, thiếu thì dùng default trong code.
    /// </summary>
    public decimal RuleScoreOf(FengShuiElement subject, FengShuiElement obj)
        => RuleScores is not null && RuleScores.TryGetValue((subject, obj), out var v)
            ? v
            : FengShuiCalculator.DefaultScore(FengShuiCalculator.GetRelation(subject, obj));
}

/// <summary>Một hướng Bát Trạch kèm tên cung, đã lọc theo mục tiêu người dùng (vd Đông Nam — Sinh Khí).</summary>
public sealed record AspirationDirection(CompassDirection Direction, string CungName);

/// <summary>Thuộc tính phong thủy của 1 sản phẩm ứng viên (đã rút &amp; dựng vector từ DB).</summary>
public sealed record ProductFacts(
    Guid ProductId,
    ElementVector Vector,
    IReadOnlySet<string> Vibes,
    ProductPlacement Placement = ProductPlacement.Desk);

/// <summary>Nguồn vector mục tiêu khi chấm điểm 1 sản phẩm.</summary>
public enum ScoringTarget
{
    /// <summary>Gap của phòng: <c>AdjustedIdeal − Current</c>.</summary>
    WorkspaceGap,

    /// <summary>Vector dụng thần/bản mệnh của người dùng (<see cref="ScoringContext.PersonalNeedVector"/>).</summary>
    PersonalNeed,
}

/// <summary>Cách xử lý hướng đặt vật phẩm.</summary>
public enum DirectionMode
{
    /// <summary>Directional Validation của engine v3: hết hướng hợp → phạt + caution.</summary>
    Soft,

    /// <summary>Không xét hướng (vật di chuyển theo người, hoặc cây đặt theo ánh sáng).</summary>
    None,
}

/// <summary>Mức độ nghiêm khắc của bộ lọc khắc bản mệnh.</summary>
public enum PersonalConflictMode
{
    /// <summary>Loại cứng khi <see cref="WorkspaceScope.Private"/>, còn lại chỉ trừ điểm (luật v3).</summary>
    ByScope,

    /// <summary>Luôn loại cứng — vật đeo trên người là riêng tư tuyệt đối.</summary>
    AlwaysHard,

    /// <summary>
    /// v3.1 — không loại, không trừ thêm: xung khắc đã được tính có dấu trong <c>personalScore</c>.
    /// Giữ cả hai sẽ tính phạt hai lần. Chỉ dùng khi <see cref="ScoringContext.PersonalBlendActive"/>.
    /// </summary>
    None,
}

/// <summary>
/// Luật chấm điểm theo <see cref="ProductPlacement"/> — khai báo dạng BẢNG thay vì nhánh <c>switch</c>
/// rải trong <c>RecommendationScorer</c>. Thêm placement mới = thêm một dòng ở <see cref="For"/>.
/// Xem <c>docs/adr/product-placement-personal-recommendation.md</c> §3.
/// </summary>
public sealed record PlacementPolicy(
    bool IsRecommendable,
    ScoringTarget Target,
    DirectionMode Direction,
    PersonalConflictMode Conflict,
    bool UsePurposeVibe)
{
    /// <summary>
    /// Luật dùng cho trang chi tiết sản phẩm (<c>ScoreSingle</c>): luôn chấm theo phòng và KHÔNG BAO GIỜ
    /// loại — placement chỉ sinh caution. Giữ đúng hợp đồng "fit luôn có kết quả".
    /// </summary>
    public static PlacementPolicy WorkspaceFit(bool personalBlendActive) =>
        new(true, ScoringTarget.WorkspaceGap, DirectionMode.Soft, WorkspaceConflict(personalBlendActive), false);

    /// <summary>
    /// Cách xử lý khắc bản mệnh cho nhánh chấm theo phòng. Trục cá nhân bật (v3.1) → xung khắc đã nằm
    /// trong <c>personalScore</c> nên không loại &amp; không trừ nữa; tắt → giữ nguyên luật v3.
    /// </summary>
    private static PersonalConflictMode WorkspaceConflict(bool personalBlendActive)
        => personalBlendActive ? PersonalConflictMode.None : PersonalConflictMode.ByScope;

    public static PlacementPolicy For(ProductPlacement placement, bool personalBlendActive = false) => placement switch
    {
        ProductPlacement.Desk =>
            new(true, ScoringTarget.WorkspaceGap, DirectionMode.Soft, WorkspaceConflict(personalBlendActive), true),

        // Cây/vật sống: vị trí do ánh sáng quyết định, không theo la bàn.
        ProductPlacement.Living =>
            new(true, ScoringTarget.WorkspaceGap, DirectionMode.None, WorkspaceConflict(personalBlendActive), true),

        // Carry KHÔNG chịu ảnh hưởng của PersonalWeight: mục tiêu của nó vốn đã 100% cá nhân.
        ProductPlacement.Carry =>
            new(true, ScoringTarget.PersonalNeed, DirectionMode.None, PersonalConflictMode.AlwaysHard, false),

        // Hàng tiêu hao: không vào bất kỳ luồng gợi ý nào.
        ProductPlacement.Consumable =>
            new(false, ScoringTarget.WorkspaceGap, DirectionMode.None, PersonalConflictMode.ByScope, false),

        _ => new(true, ScoringTarget.WorkspaceGap, DirectionMode.Soft, WorkspaceConflict(personalBlendActive), true),
    };
}

/// <summary>Kết quả chấm điểm 1 sản phẩm + các "sự thật" để AI diễn giải + gợi ý vị trí đặt.</summary>
public sealed record ScoredProduct(
    Guid ProductId,
    decimal Score,
    IReadOnlyList<string> MatchFacts,
    IReadOnlyList<string> CautionFacts,
    string? PlacementHint);
