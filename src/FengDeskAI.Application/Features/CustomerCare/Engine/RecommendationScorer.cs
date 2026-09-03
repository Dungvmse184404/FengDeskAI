using FengDeskAI.Domain.Enums.Catalog;
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

        // Fit = trang chi tiết sản phẩm × 1 phòng: luôn chấm theo phòng và không bao giờ loại (hợp đồng
        // của ScoreSingle) — placement chỉ sinh caution để user hiểu vật phẩm không dành cho không gian.
        // Trục cá nhân (v3.1) chỉ bật khi có trọng số > 0 VÀ user có ngày sinh — tắt thì mọi luật giữ như trước.
        bool personalBlend = ctx.PersonalBlendActive;
        var policy = mode == ScoreMode.Fit
            ? PlacementPolicy.WorkspaceFit(personalBlend)
            : PlacementPolicy.For(product.Placement, personalBlend);

        if (!policy.IsRecommendable)
            return null; // vd hàng tiêu hao — không đưa vào bất kỳ danh sách gợi ý nào

        if (mode == ScoreMode.Fit)
            DescribePlacementForFit(product.Placement, cautions);

        // ── Bước 2a — Intent: phân biệt "lệch vibe" với "chưa khai vibe" (thiếu dữ liệu ≠ bằng chứng lệch).
        //    VIBE_FILTER_HARD ≥ 0.5 giữ nguyên hành vi v3 (loại cứng); < 0.5 chuyển sang trừ điểm.
        decimal vibePenalty = 0m;
        if (TargetVibe(ctx.Purpose) is { } vibe)
        {
            bool unknown = product.Vibes.Count == 0;
            bool mismatch = !unknown && !product.Vibes.Contains(vibe);

            // Fit không bao giờ loại (hợp đồng ScoreSingle) nhưng vẫn phải nói ra lý do.
            if ((unknown || mismatch) && (mode == ScoreMode.Fit || policy.UsePurposeVibe))
            {
                bool hard = ctx.Params.VibeFilterHard >= 0.5m;
                if (mode == ScoreMode.Rank && hard)
                    return null; // loại khỏi candidates

                if (!hard)
                    vibePenalty = unknown ? ctx.Params.VibeUnknownPenalty : ctx.Params.VibeMismatchPenalty;

                cautions.Add(unknown
                    ? "Sản phẩm chưa khai báo vibe — chưa xác minh được có hợp mục đích phòng không."
                    : $"Vibe sản phẩm chưa khớp mục đích {ctx.Purpose} của phòng.");
            }
        }

        var productDominant = product.Vector.Dominant();

        // ── Bước 2b — User constraint (chỉ khi có personalVector VÀ policy còn dùng luật phạt/loại) ──
        //    PersonalConflictMode.None = v3.1 đã tính xung khắc có dấu trong personalScore ở bước 2c.
        decimal userPenalty = 0m;
        if (ctx.PersonalVector is { } personal && policy.Conflict != PersonalConflictMode.None)
        {
            var personalDominant = personal.Dominant();
            // Hành trội sản phẩm KHẮC mệnh user → xét quan hệ từ mệnh: BiKhac = bị obj khắc.
            bool conflict = FengShuiCalculator.GetRelation(personalDominant, productDominant) == FengShuiRelation.BiKhac;
            if (conflict)
            {
                bool hardFilter = policy.Conflict == PersonalConflictMode.AlwaysHard
                    || ctx.Scope == WorkspaceScope.Private;

                if (mode == ScoreMode.Rank && hardFilter)
                    return null; // hard: loại khỏi candidates
                userPenalty = ctx.Params.UserConflictPenalty; // soft: trừ điểm
                cautions.Add($"Hành {productDominant} khắc bản mệnh {personalDominant} — trừ điểm"
                    + (ctx.Scope == WorkspaceScope.Private ? " (không gian riêng tư)." : " (không gian dùng chung)."));
            }
            else
            {
                facts.Add($"Hành {productDominant} không khắc bản mệnh {personalDominant}.");
            }
        }

        // ── Bước 2c — Điểm khớp vector mục tiêu (gap phòng HOẶC dụng thần người) ──
        var (target, targetL1) = policy.Target == ScoringTarget.PersonalNeed
            ? (ctx.PersonalNeedVector ?? ElementVector.Zero, (ctx.PersonalNeedVector ?? ElementVector.Zero).L1())
            : (gap, gapL1);

        decimal gapScore = targetL1 == 0m ? 0m : target.Dot(product.Vector) / targetL1;
        DescribeTarget(policy.Target, target, product.Vector, productDominant, gapScore, facts, cautions);

        // ── Bước 2d (v3.1) — trộn trục cá nhân, CHỈ cho nhánh chấm theo phòng ──
        //    Nhánh PersonalNeed (Carry) vốn đã 100% cá nhân nên không trộn thêm.
        decimal blended = gapScore;
        if (personalBlend && policy.Target == ScoringTarget.WorkspaceGap && ctx.PersonalVector is { } personalVec)
        {
            var personalDominant = personalVec.Dominant();
            decimal personalScore = PersonalAffinity(ctx, personalDominant, product.Vector);
            blended = (1m - ctx.PersonalWeight) * gapScore + ctx.PersonalWeight * personalScore;
            DescribePersonalAffinity(personalDominant, productDominant, personalScore, facts, cautions);
        }

        // ── Bước 3 — Directional Validation (bỏ qua với vật mang theo người / cây) ──
        var (dirPenalty, placementHint) = policy.Direction == DirectionMode.Soft
            ? ValidateDirection(ctx, productDominant)
            : (0m, PlacementHintWithoutDirection(product.Placement));

        decimal score = Math.Round(Math.Clamp(blended - userPenalty - dirPenalty - vibePenalty, -1m, 1m), 3);

        // Lưới an toàn: loại theo ĐIỂM TỔNG thay vì theo từng thuộc tính đơn lẻ. Fit không cắt —
        // trang chi tiết sản phẩm luôn phải trả về kết quả kể cả khi điểm rất thấp.
        if (mode == ScoreMode.Rank && score < ctx.Params.MinScoreThreshold)
            return null;

        return new ScoredProduct(product.ProductId, score, facts, cautions, placementHint);
    }

    /// <summary>
    /// Điểm hợp mệnh của sản phẩm, ∈ [−1, +1] — v3.1. Tổng có TRỌNG SỐ theo vector sản phẩm của điểm
    /// quan hệ ngũ hành từ <c>feng_shui_rules</c>:
    /// <code>personalScore = Σ productVector[e] × RuleScore(mệnh user, e)</code>
    /// <para>
    /// Cố tình KHÔNG dùng tích vô hướng <c>personalVector · productVector</c>: hai vector đều không âm
    /// nên kết quả luôn ≥ 0 — không bao giờ phạt được sản phẩm khắc mệnh, trục cá nhân sẽ vô nghĩa.
    /// Bảng luật cho điểm CÓ DẤU (tỷ hòa +1.0 … bị khắc −1.0) nên diễn đạt được cả hai chiều.
    /// </para>
    /// </summary>
    private static decimal PersonalAffinity(
        ScoringContext ctx, FengShuiElement personalDominant, ElementVector productVector)
    {
        decimal sum = 0m;
        foreach (var (element, weight) in productVector.Enumerate())
        {
            if (weight == 0m) continue;
            sum += weight * ctx.RuleScoreOf(personalDominant, element);
        }

        return Math.Clamp(sum, -1m, 1m);
    }

    /// <summary>Sự thật để AI diễn giải phần điểm cá nhân — thay cho caution "khắc bản mệnh" của luật cũ.</summary>
    private static void DescribePersonalAffinity(
        FengShuiElement personalDominant, FengShuiElement productDominant, decimal personalScore,
        List<string> facts, List<string> cautions)
    {
        var relation = FengShuiCalculator.GetRelation(personalDominant, productDominant);

        if (personalScore > 0.05m)
        {
            facts.Add(relation switch
            {
                FengShuiRelation.TuongHoa =>
                    $"Hành {productDominant} tỷ hòa với bản mệnh {personalDominant} của bạn.",
                FengShuiRelation.TuongSinh =>
                    $"Hành {productDominant} tương sinh, nuôi dưỡng bản mệnh {personalDominant} của bạn.",
                _ => $"Hành {productDominant} thuận với bản mệnh {personalDominant} của bạn.",
            });
        }
        else if (personalScore < -0.05m)
        {
            cautions.Add(relation == FengShuiRelation.BiKhac
                ? $"Hành {productDominant} khắc bản mệnh {personalDominant} — đã trừ vào điểm hợp mệnh."
                : $"Hành {productDominant} làm hao khí bản mệnh {personalDominant} — trừ nhẹ điểm hợp mệnh.");
        }
    }

    /// <summary>Gợi ý vị trí cho nhóm không xét hướng la bàn — thay chỗ của Directional Validation.</summary>
    private static string? PlacementHintWithoutDirection(ProductPlacement placement) => placement switch
    {
        ProductPlacement.Carry =>
            "Vật phẩm mang theo người — tác dụng đi theo bản mệnh của bạn, không phụ thuộc hướng đặt trong phòng.",
        ProductPlacement.Living =>
            "Cây/vật sống — ưu tiên vị trí đủ ánh sáng và dễ chăm sóc thay vì chọn theo hướng la bàn.",
        _ => null,
    };

    /// <summary>Caution cho trang Fit khi sản phẩm vốn không phải đồ đặt trong không gian.</summary>
    private static void DescribePlacementForFit(ProductPlacement placement, List<string> cautions)
    {
        switch (placement)
        {
            case ProductPlacement.Carry:
                cautions.Add("Đây là vật phẩm mang theo người — điểm dưới đây chấm theo nhu cầu của phòng, "
                    + "để chọn đúng nên xem gợi ý theo bản mệnh.");
                break;
            case ProductPlacement.Consumable:
                cautions.Add("Đây là hàng tiêu hao, cần thay định kỳ — không được engine đưa vào danh sách gợi ý.");
                break;
        }
    }

    /// <summary>
    /// Diễn giải điểm khớp. Cùng phép tính cho cả hai chế độ, chỉ khác chủ ngữ: gap phòng nói về
    /// "phòng đang thiếu/thừa", vector cá nhân nói về "hành bản mệnh cần được bồi".
    /// </summary>
    private static void DescribeTarget(
        ScoringTarget targetKind, ElementVector target, ElementVector productVector,
        FengShuiElement productDominant, decimal gapScore, List<string> facts, List<string> cautions)
    {
        // Top hành đang thiếu (giá trị dương lớn nhất) mà sản phẩm bù được.
        var topNeeded = target.Enumerate().Where(x => x.Value > 0)
            .OrderByDescending(x => x.Value).Take(2).Select(x => x.Element).ToList();
        var topExcess = target.Enumerate().Where(x => x.Value < 0)
            .OrderBy(x => x.Value).Take(2).Select(x => x.Element).ToList();

        bool personal = targetKind == ScoringTarget.PersonalNeed;

        if (gapScore >= 0)
        {
            var bumped = topNeeded.Where(e => productVector[e] > 0m).ToList();
            if (bumped.Count > 0)
                facts.Add(personal
                    ? $"Mang hành {string.Join("/", bumped)} — đúng hành bản mệnh bạn cần được bồi."
                    : $"Bù năng lượng hành {string.Join("/", bumped)} đang thiếu của phòng.");
            else
                facts.Add(personal
                    ? $"Hành trội {productDominant} hài hòa với bản mệnh của bạn."
                    : $"Hành trội {productDominant} hài hòa với nhu cầu của phòng.");
        }
        else
        {
            // Vector cá nhân đã chuẩn hóa (Σ=1, không có phần tử âm) nên nhánh này thực tế chỉ xảy ra
            // với gap phòng; giữ lời văn riêng phòng khi không có hành thừa nào khớp.
            var worsened = topExcess.Where(e => productVector[e] > 0m).ToList();
            if (worsened.Count > 0)
                cautions.Add($"Bơm thêm hành {string.Join("/", worsened)} vốn đã thừa trong phòng — nên cân nhắc.");
            else
                cautions.Add(personal
                    ? "Hành của vật phẩm chưa khớp hành bản mệnh bạn đang cần."
                    : "Chưa bù đúng hành phòng đang thiếu.");
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
        {
            // v3.1 — user đã nêu mục tiêu: trong các hướng hợp vật phẩm, ưu tiên hướng trùng cung Bát Trạch
            // ứng với mục tiêu đó. Không có giao nhau (hoặc user không nêu) → giữ hành vi cũ.
            var preferred = ctx.AspirationDirections.FirstOrDefault(a => placementDirs.Contains(a.Direction));
            if (preferred is not null)
                return (0m, $"Hãy đặt vật phẩm này ở hướng {FengShuiCalculator.DirectionVi(preferred.Direction)} "
                    + $"— cung {preferred.CungName} của bạn, đúng mục tiêu đang hướng tới.");

            return (0m, $"Hãy đặt vật phẩm này ở hướng {DirectionVi(placementDirs[0])} của phòng để kích hoạt năng lượng tốt nhất.");
        }

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

    /// <summary>Ủy quyền về <see cref="FengShuiCalculator.DirectionVi"/> — giữ một nguồn tên hướng duy nhất.</summary>
    private static string DirectionVi(CompassDirection d) => FengShuiCalculator.DirectionVi(d);
}
