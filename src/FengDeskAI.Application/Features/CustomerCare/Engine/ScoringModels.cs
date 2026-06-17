using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Trọng số chấm điểm. Phần cá nhân (Element+Direction) sẽ bị nhân với PersonalWeight của
/// loại không gian; phần chức năng (Purpose/Style/Lighting/Size) luôn tính nguyên.
/// </summary>
public sealed record ScoringWeights
{
    public decimal Element { get; init; } = 0.6m;
    public decimal Direction { get; init; } = 0.4m;
    public decimal Purpose { get; init; } = 0.4m;
    public decimal Style { get; init; } = 0.2m;
    public decimal Lighting { get; init; } = 0.2m;
    public decimal Size { get; init; } = 0.2m;

    public static ScoringWeights Default { get; } = new();
}

/// <summary>
/// Hồ sơ cá nhân. <see cref="Element"/> (mệnh Nạp Âm) tính chỉ từ năm sinh — luôn có khi có ngày sinh.
/// Kua/hướng cần thêm giới tính Nam/Nữ; thiếu giới tính → <see cref="KuaNumber"/>/<see cref="Group"/> null
/// và <see cref="FavorableDirections"/> rỗng (bỏ phần hướng, vẫn giữ phần mệnh).
/// </summary>
public sealed record PersonalProfile(
    FengShuiElement Element,
    int? KuaNumber,
    KuaGroup? Group,
    IReadOnlySet<CompassDirection> FavorableDirections);

/// <summary>Bối cảnh không gian + cá nhân cho 1 phiên chấm điểm.</summary>
public sealed record ScoringContext
{
    public required decimal PersonalWeight { get; init; }
    public required WorkPurpose Purpose { get; init; }
    /// <summary>Mã phong cách mong muốn (styles.code), vd "Minimal".</summary>
    public required string Style { get; init; }
    public required LightingType Lighting { get; init; }
    public required CompassDirection DeskOrientation { get; init; }
    public required int DeskArea { get; init; }

    /// <summary>Null → bỏ qua toàn bộ điểm cá nhân (mệnh + hướng).</summary>
    public PersonalProfile? Personal { get; init; }

    public ScoringWeights Weights { get; init; } = ScoringWeights.Default;

    /// <summary>
    /// Điểm element theo cặp (mệnh người, hành sản phẩm) nạp từ feng_shui_rules.
    /// Thiếu cặp nào thì fallback <see cref="FengShuiCalculator.DefaultScore"/>.
    /// </summary>
    public IReadOnlyDictionary<(FengShuiElement Subject, FengShuiElement Object), decimal> ElementScores { get; init; }
        = new Dictionary<(FengShuiElement, FengShuiElement), decimal>();
}

/// <summary>Thuộc tính phong thủy của 1 sản phẩm ứng viên (đã rút từ DB).</summary>
public sealed record ProductFacts(
    Guid ProductId,
    FengShuiElement? PrimaryElement,
    FengShuiElement? SecondaryElement,
    SizeClass SizeClass,
    IReadOnlySet<string> Vibes,
    IReadOnlySet<string> Styles);

/// <summary>Kết quả chấm điểm 1 sản phẩm + các "sự thật" để AI diễn giải.</summary>
public sealed record ScoredProduct(
    Guid ProductId,
    decimal Score,
    IReadOnlyList<string> MatchFacts,
    IReadOnlyList<string> CautionFacts);
