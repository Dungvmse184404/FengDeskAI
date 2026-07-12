using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>Mã vibe canonical mà thuật toán quan tâm (khớp vibes.code đã seed).</summary>
internal static class VibeCodes
{
    public const string Focus = "Focus";
    public const string Relax = "Relax";
    public const string Creative = "Creative";
    public const string Calm = "Calm";
    public const string Energize = "Energize";
}

/// <summary>
/// Engine v3 — 3 bước theo PHẦN D của spec: Gap → lọc &amp; chấm khớp → Directional Validation.
/// Sản phẩm là "viên thuốc" bù mất cân bằng của phòng; bơm vào hành thiếu → dương, hành thừa → âm.
/// </summary>
public sealed class RecommendationScorer : IRecommendationScorer
{
    public IReadOnlyList<ScoredProduct> Score(ScoringContext context, IReadOnlyList<ProductFacts> candidates)
    {
        // Bước 1 — Gap (mảnh ghép còn thiếu). + = thiếu cần bù, − = thừa cần tránh.
        var gap = context.AdjustedIdeal.Subtract(context.CurrentVector);
        decimal gapL1 = gap.L1();

        var results = new List<ScoredProduct>(candidates.Count);
        foreach (var product in candidates)
        {
            var scored = ScoreOne(context, product, gap, gapL1, ScoreMode.Rank);
            if (scored is not null)
                results.Add(scored);
        }

        return results.OrderByDescending(r => r.Score).ToList();
    }

    public ScoredProduct ScoreSingle(ScoringContext context, ProductFacts product)
    {
        var gap = context.AdjustedIdeal.Subtract(context.CurrentVector);
        decimal gapL1 = gap.L1();

        // Mode Fit không bao giờ loại — ScoreOne luôn trả về non-null ở nhánh này.
        return ScoreOne(context, product, gap, gapL1, ScoreMode.Fit)!;
    }

    /// <summary>Rank: chấm để xếp hạng &amp; lọc candidates. Fit: chấm 1×1 cho trang chi tiết — không loại, chỉ caution.</summary>
    private enum ScoreMode { Rank, Fit }

    private static ScoredProduct? ScoreOne(ScoringContext ctx, ProductFacts product, ElementVector gap, decimal gapL1, ScoreMode mode)
    {
        var facts = new List<string>();
        var cautions = new List<string>();

        // ── Bước 2a — Intent filter: Rank loại khỏi candidates; Fit chỉ ghi caution ──
        var targetVibe = TargetVibe(ctx.Purpose);
        if (targetVibe is { } vibe && !product.Vibes.Contains(vibe))
        {
            if (mode == ScoreMode.Rank)
                return null; // loại khỏi candidates
            cautions.Add($"Sản phẩm chưa thuộc nhóm vibe phù hợp mục đích {ctx.Purpose} của phòng.");
        }

        var productDominant = product.Vector.Dominant();

        // ── Bước 2b — User constraint (chỉ khi có personalVector) ──
        decimal userPenalty = 0m;
        if (ctx.PersonalVector is { } personal)
        {
            var personalDominant = personal.Dominant();
            // Hành trội sản phẩm KHẮC mệnh user → xét quan hệ từ mệnh: BiKhac = bị obj khắc.
            bool conflict = FengShuiCalculator.GetRelation(personalDominant, productDominant) == FengShuiRelation.BiKhac;
            if (conflict)
            {
                if (mode == ScoreMode.Rank && ctx.Scope == WorkspaceScope.Private)
                    return null; // hard: loại khỏi candidates (chỉ khi rank cho không gian riêng tư)
                userPenalty = ctx.Params.UserConflictPenalty; // soft: trừ điểm
                cautions.Add($"Hành {productDominant} khắc bản mệnh {personalDominant} — trừ điểm"
                    + (ctx.Scope == WorkspaceScope.Private ? " (không gian riêng tư)." : " (không gian dùng chung)."));
            }
            else
            {
                facts.Add($"Hành {productDominant} không khắc bản mệnh {personalDominant}.");
            }
        }

        // ── Bước 2c — Điểm khớp Gap ──
        decimal gapScore = gapL1 == 0m ? 0m : gap.Dot(product.Vector) / gapL1;
        DescribeGap(gap, product.Vector, productDominant, gapScore, facts, cautions);

        // ── Bước 3 — Directional Validation ──
        var (dirPenalty, placementHint) = ValidateDirection(ctx, productDominant);

        decimal score = Math.Round(Math.Clamp(gapScore - userPenalty - dirPenalty, -1m, 1m), 3);
        return new ScoredProduct(product.ProductId, score, facts, cautions, placementHint);
    }

    private static void DescribeGap(
        ElementVector gap, ElementVector productVector, FengShuiElement productDominant,
        decimal gapScore, List<string> facts, List<string> cautions)
    {
        // Top hành đang thiếu (gap dương lớn nhất) mà sản phẩm bù được.
        var topNeeded = gap.Enumerate().Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value).Take(2).Select(x => x.Element).ToList();
        var topExcess = gap.Enumerate().Where(x => x.Value < 0)
            .OrderBy(x => x.Value).Take(2).Select(x => x.Element).ToList();

        if (gapScore >= 0)
        {
            var bumped = topNeeded.Where(e => productVector[e] > 0m).ToList();
            facts.Add(bumped.Count > 0
                ? $"Bù năng lượng hành {string.Join("/", bumped)} đang thiếu của phòng."
                : $"Hành trội {productDominant} hài hòa với nhu cầu của phòng.");
        }
        else
        {
            var worsened = topExcess.Where(e => productVector[e] > 0m).ToList();
            cautions.Add(worsened.Count > 0
                ? $"Bơm thêm hành {string.Join("/", worsened)} vốn đã thừa trong phòng — nên cân nhắc."
                : $"Chưa bù đúng hành phòng đang thiếu.");
        }
    }

    /// <summary>
    /// Hướng hợp vật phẩm = hướng cùng hành trội ∪ hướng SINH ra hành trội, trừ đi hướng bị chắn.
    /// Còn hướng hợp → không phạt + hint; không còn → phạt DIRECTION_PENALTY.
    /// </summary>
    private static (decimal Penalty, string? Hint) ValidateDirection(ScoringContext ctx, FengShuiElement productDominant)
    {
        var generating = FengShuiCalculator.GetGeneratingElement(productDominant);
        var goodDirs = FengShuiCalculator.GetDirectionsForElement(productDominant)
            .Concat(FengShuiCalculator.GetDirectionsForElement(generating))
            .ToHashSet();

        var placementDirs = goodDirs.Where(d => !ctx.ViolatedDirections.Contains(d)).ToList();

        if (placementDirs.Count > 0)
            return (0m, $"Hãy đặt vật phẩm này ở hướng {DirectionVi(placementDirs[0])} của phòng để kích hoạt năng lượng tốt nhất.");

        return (ctx.Params.DirectionPenalty,
            "Các hướng hợp với vật phẩm đều bị chắn (cửa/WC/góc tối) — cân nhắc vị trí đặt.");
    }

    private static string? TargetVibe(WorkPurpose purpose) => purpose switch
    {
        WorkPurpose.Office => VibeCodes.Focus,
        WorkPurpose.Study => VibeCodes.Focus,
        WorkPurpose.Reading => VibeCodes.Calm,
        WorkPurpose.Creative => VibeCodes.Creative,
        WorkPurpose.Gaming => VibeCodes.Energize,
        WorkPurpose.Cooking => VibeCodes.Energize,
        WorkPurpose.Dining => VibeCodes.Relax,
        WorkPurpose.Relaxation => VibeCodes.Relax,
        WorkPurpose.Sleep => VibeCodes.Calm,
        WorkPurpose.Childcare => VibeCodes.Calm,
        WorkPurpose.Exercise => VibeCodes.Energize,
        WorkPurpose.Mixed => VibeCodes.Focus,
        _ => null, // Other → không lọc theo intent
    };

    private static string DirectionVi(CompassDirection d) => d switch
    {
        CompassDirection.North => "Bắc",
        CompassDirection.Northeast => "Đông Bắc",
        CompassDirection.East => "Đông",
        CompassDirection.Southeast => "Đông Nam",
        CompassDirection.South => "Nam",
        CompassDirection.Southwest => "Tây Nam",
        CompassDirection.West => "Tây",
        CompassDirection.Northwest => "Tây Bắc",
        _ => d.ToString(),
    };
}
