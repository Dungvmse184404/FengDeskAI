using FengDeskAI.Domain.Entities.Recommendation;
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

    public required WorkspaceScope Scope { get; init; }
    public required WorkPurpose Purpose { get; init; }

    /// <summary>Hướng bị chắn (cửa vào ∪ WC ∪ góc tối) — dùng ở Directional Validation.</summary>
    public IReadOnlySet<CompassDirection> ViolatedDirections { get; init; } = new HashSet<CompassDirection>();

    public ScoringParameters Params { get; init; } = ScoringParameters.Default;
}

/// <summary>Thuộc tính phong thủy của 1 sản phẩm ứng viên (đã rút &amp; dựng vector từ DB).</summary>
public sealed record ProductFacts(
    Guid ProductId,
    ElementVector Vector,
    IReadOnlySet<string> Vibes);

/// <summary>Kết quả chấm điểm 1 sản phẩm + các "sự thật" để AI diễn giải + gợi ý vị trí đặt.</summary>
public sealed record ScoredProduct(
    Guid ProductId,
    decimal Score,
    IReadOnlyList<string> MatchFacts,
    IReadOnlyList<string> CautionFacts,
    string? PlacementHint);
