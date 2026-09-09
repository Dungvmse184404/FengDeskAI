using FengDeskAI.Domain.Entities.CustomerCare;
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

    /// <summary>
    /// Tên tiếng Việt của cảm hứng không gian — <b>mã code không được lên giao diện</b>.
    ///
    /// <para>
    /// Bảng <c>vibes</c> có cột <c>name</c> tiếng Việt và admin sửa được, nhưng engine là code THUẦN
    /// (không I/O) nên không đọc DB được. Giữ bản dịch tại đây cho 5 mã mà engine tự suy ra từ
    /// <c>WorkPurpose</c>; mã lạ thì trả về chính nó thay vì ném lỗi — một nhãn hơi thô vẫn hơn là
    /// làm hỏng cả lượt chấm điểm.
    /// </para>
    /// </summary>
    public static string Vi(string code) => code switch
    {
        Focus => "tập trung",
        Relax => "thư giãn",
        Creative => "sáng tạo",
        Calm => "tĩnh tại",
        Energize => "năng lượng",
        _ => code,
    };
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

    public ScoredProduct ScoreSinglePersonal(ScoringContext context, ProductFacts product)
    {
        // Không có phòng: gap = Zero − Zero. Policy của Carry không đọc tới hai vector này.
        var gap = context.AdjustedIdeal.Subtract(context.CurrentVector);
        return ScoreOne(context, product, gap, gap.L1(), ScoreMode.PersonalFit)!;
    }

    /// <summary>
    /// Rank: chấm để xếp hạng &amp; lọc candidates. Fit: chấm 1×1 theo phòng cho trang chi tiết — không
    /// loại, chỉ caution. PersonalFit: chấm 1×1 theo bản mệnh (vật mang theo người) — cũng không loại.
    /// <para>Chỉ <see cref="Rank"/> mới được loại sản phẩm; hai mode Fit giữ hợp đồng "luôn có kết quả".</para>
    /// </summary>
    private enum ScoreMode { Rank, Fit, PersonalFit }

    private static ScoredProduct? ScoreOne(ScoringContext ctx, ProductFacts product, ElementVector gap, decimal gapL1, ScoreMode mode)
    {
        var facts = new List<string>();
        var cautions = new List<string>();

        // Fit = trang chi tiết sản phẩm × 1 phòng: luôn chấm theo phòng và không bao giờ loại (hợp đồng
        // của ScoreSingle) — placement chỉ sinh caution để user hiểu vật phẩm không dành cho không gian.
        // Trục cá nhân (v3.1) chỉ bật khi có trọng số > 0 VÀ user có ngày sinh — tắt thì mọi luật giữ như trước.
        bool personalBlend = ctx.PersonalBlendActive;
        var policy = mode switch
        {
            ScoreMode.Fit => PlacementPolicy.WorkspaceFit(personalBlend, ctx.Scope),
            // Ép luật của Carry bất kể placement thật: endpoint fit/personal trả lời "vật này hợp bản
            // mệnh bạn tới đâu", nên phải chấm theo dụng thần chứ không theo gap phòng.
            ScoreMode.PersonalFit => PlacementPolicy.For(ProductPlacement.Carry, personalBlend, ctx.Scope),
            _ => PlacementPolicy.For(product.Placement, personalBlend, ctx.Scope),
        };

        if (!policy.IsRecommendable)
            return null; // vd hàng tiêu hao — không đưa vào bất kỳ danh sách gợi ý nào

        if (mode == ScoreMode.Fit)
            DescribePlacementForFit(product.Placement, cautions);

        // ── Bước 2a — Intent: phân biệt "lệch vibe" với "chưa khai vibe" (thiếu dữ liệu ≠ bằng chứng lệch).
        //    VIBE_FILTER_HARD ≥ 0.5 giữ nguyên hành vi v3 (loại cứng); < 0.5 chuyển sang trừ điểm.
        decimal vibePenalty = 0m;
        string vibeCode = ScoringParamCodes.VibeMismatchPenalty;
        string vibeReason = "Cảm hứng không gian của sản phẩm hợp mục đích của phòng - không trừ điểm.";
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

                vibeCode = unknown ? ScoringParamCodes.VibeUnknownPenalty : ScoringParamCodes.VibeMismatchPenalty;
                // Cả tên mục đích lẫn tên cảm hứng đều phải là tiếng Việt: `ctx.Purpose` in ra thẳng
                // sẽ cho user thấy "Cooking", còn `vibe` sẽ cho thấy "Energize" - tên hằng trong code.
                string purposeVi = ElementSemantics.PurposeVi(ctx.Purpose);
                string vibeVi = VibeCodes.Vi(vibe);

                vibeReason = unknown
                    ? $"Sản phẩm chưa khai cảm hứng không gian nào - chưa xác minh được có hợp mục đích {purposeVi} của phòng không."
                    : $"Phòng dùng để {purposeVi} nên cần cảm hứng {vibeVi}; sản phẩm không khai mục đó.";

                cautions.Add(unknown
                    ? "Sản phẩm chưa khai cảm hứng không gian - chưa xác minh được có hợp mục đích phòng không."
                    : $"Cảm hứng không gian của sản phẩm chưa khớp mục đích {purposeVi} của phòng.");
            }
        }
        else
        {
            vibeReason = "Phòng không nêu mục đích cụ thể - không xét cảm hứng không gian.";
        }

        var productDominant = product.Vector.Dominant();

        // ── Bước 2b — User constraint (chỉ khi có personalVector VÀ policy còn dùng luật phạt/loại) ──
        //    PersonalConflictMode.None = không gian Public: không neo vào bản mệnh một người (§14.3 · Q12).
        //    Scaled = L2 của v3.2: không loại, trừ USER_CONFLICT_PENALTY × Wp (§14.2).
        decimal userPenalty = 0m;
        string userPenaltyCode = ScoringParamCodes.UserConflictPenalty;
        string userPenaltyLabel = "Khắc bản mệnh";
        string userPenaltyReason = policy.Conflict switch
        {
            PersonalConflictMode.None when ctx.Scope == WorkspaceScope.Public =>
                "Không gian chung - hệ thống không xét khắc bản mệnh của riêng ai.",
            _ when ctx.PersonalVector is null =>
                "Chưa có ngày sinh nên chưa xác định được bản mệnh để xét.",
            _ => "Hành trội của sản phẩm không khắc bản mệnh của bạn.",
        };
        if (ctx.PersonalVector is { } personal && policy.Conflict != PersonalConflictMode.None)
        {
            bool scaled = policy.Conflict == PersonalConflictMode.Scaled;
            var personalDominant = personal.Dominant();
            // Hành trội sản phẩm KHẮC mệnh user → xét quan hệ từ mệnh: BiKhac = bị obj khắc.
            // Nhị phân theo hành TRỘI, không theo tỉ trọng — nhất quán với luật v3 và với
            // DescribePersonalAffinity (§14.6 #2).
            bool conflict = FengShuiCalculator.GetRelation(personalDominant, productDominant) == FengShuiRelation.BiKhac;
            if (conflict)
            {
                // Scaled không bao giờ loại: phần "bị khắc" đã thành một số hạng có tên trong điểm,
                // giữ sản phẩm lại để còn giải thích được vì sao nó xếp thấp (R1).
                bool hardFilter = !scaled
                    && (policy.Conflict == PersonalConflictMode.AlwaysHard || ctx.Scope == WorkspaceScope.Private);

                if (mode == ScoreMode.Rank && hardFilter)
                    return null; // hard: loại khỏi candidates

                userPenalty = scaled
                    ? ctx.Params.UserConflictPenalty * ctx.PersonalWeight
                    : ctx.Params.UserConflictPenalty;

                userPenaltyReason = scaled
                    ? $"Hành {productDominant} khắc bản mệnh {personalDominant}. Mức phạt co giãn theo "
                        + $"trọng số cá nhân của không gian: {ctx.Params.UserConflictPenalty:0.00} × "
                        + $"{ctx.PersonalWeight:0.00} = {userPenalty:0.00}."
                    : $"Hành {productDominant} khắc bản mệnh {personalDominant} - trục cá nhân đang tắt "
                        + $"nên áp mức phạt đầy đủ {userPenalty:0.00}.";

                cautions.Add(scaled
                    ? $"Hành {productDominant} khắc bản mệnh {personalDominant} - trừ {userPenalty:0.00} điểm."
                    : $"Hành {productDominant} khắc bản mệnh {personalDominant} - trừ điểm"
                        + (ctx.Scope == WorkspaceScope.Private ? " (không gian riêng tư)." : " (không gian dùng chung)."));
            }
            else if (policy.Target == ScoringTarget.PersonalNeed
                     && ctx.Params.MinorClashPenalty > 0m
                     && ClashShare(personalDominant, product.Vector) is > 0m and var clashShare)
            {
                // §18 — hành trội không khắc, nhưng vật vẫn CHỨA hành khắc mệnh. Chỉ nhánh dụng thần
                // mới cần nhánh này: vector dụng thần không âm nên phần khắc đó nhân ra đúng 0, còn
                // luồng phòng đã trừ nó qua r có dấu.
                userPenalty = ctx.Params.MinorClashPenalty * clashShare;
                userPenaltyCode = ScoringParamCodes.MinorClashPenalty;
                userPenaltyLabel = "Khắc bản mệnh (phần phụ)";

                string clashing = ClashingElementsVi(personalDominant, product.Vector);
                userPenaltyReason =
                    $"Hành trội {ElementSemantics.ElementName(productDominant)} không khắc bản mệnh "
                    + $"{ElementSemantics.ElementName(personalDominant)} của bạn, nhưng vật phẩm còn "
                    + $"{clashShare:P0} là {clashing} - khắc bản mệnh. Vật mang trên người nên phần đó "
                    + $"vẫn bị trừ theo đúng tỉ trọng: {ctx.Params.MinorClashPenalty:0.00} × "
                    + $"{clashShare:0.00} = {userPenalty:0.00}.";

                cautions.Add(
                    $"Vật phẩm có {clashShare:P0} {clashing} - khắc bản mệnh "
                    + $"{ElementSemantics.ElementName(personalDominant)} của bạn, trừ {userPenalty:0.00} điểm.");
            }
            else if (!scaled)
            {
                // Ở chế độ Scaled, DescribePersonalAffinity (bước 2d) đã nói về quan hệ mệnh ↔ sản phẩm
                // với nhiều thông tin hơn — thêm dòng này nữa là lặp.
                facts.Add($"Hành {productDominant} không khắc bản mệnh {personalDominant}.");
            }
        }

        // ── Bước 2c — Điểm khớp vector mục tiêu (gap phòng HOẶC dụng thần người) ──
        var (target, targetL1) = policy.Target == ScoringTarget.PersonalNeed
            ? (ctx.PersonalNeedVector ?? ElementVector.Zero, (ctx.PersonalNeedVector ?? ElementVector.Zero).L1())
            : (gap, gapL1);

        // v3.2 §8 — chuẩn hoá về ±1.0. Mẫu số phải theo NHÁNH, không chia đôi tất:
        //   WorkspaceGap : target = gap, Σ = 0 ⇒ nửa dương = |gap|₁/2, tử số chỉ với tới nửa đó
        //                  ⇒ chia |gap|₁ (cũ) kẹp trần ở ±0.5, tier "Rất hợp" (≥0.6) bất khả thi.
        //   PersonalNeed : target Σ = 1 và KHÔNG âm ⇒ không có "hai nửa" để chia. Chia đôi ở đây thì
        //                  mọi sản phẩm khớp ≥50% đều bão hoà 1.000 ⇒ luồng Carry mất khả năng xếp hạng.
        // ĝ / r / d dựng bằng ElementDirection — CÙNG một hàm mà radar phân tích phòng dùng, nên đa giác
        // trên hai màn hình không thể lệch nhau. Chuẩn hoá VECTOR trước rồi mới nhân (thay vì chia điểm
        // vô hướng sau) để ĝ trở thành đại lượng có thật, trả được ra ngoài cho FE mô phỏng Wp (§10.3).
        FengShuiElement? destiny = ctx.PersonalVector?.Dominant();
        bool blendPersonal = personalBlend && policy.Target == ScoringTarget.WorkspaceGap;

        var direction = policy.Target == ScoringTarget.PersonalNeed
            ? ElementDirection.ForPersonalNeed(target)
            : ElementDirection.ForWorkspaceGap(
                target, blendPersonal ? destiny : null, ctx.PersonalWeight, ctx.RuleScoreOf,
                ctx.OccupationDelta, ctx.Params.OccupationShare);

        var normalizedGap = direction.NormalizedGap;
        var ruleScoreVector = direction.RuleScoreVector;
        var combinedDirection = direction.CombinedDirection;

        decimal gapScore = Math.Clamp(normalizedGap.Dot(product.Vector), -1m, 1m);
        DescribeTarget(policy.Target, target, product.Vector, productDominant, gapScore, facts, cautions);

        // ── Bước 2d (v3.1) — trộn trục cá nhân, CHỈ cho nhánh chấm theo phòng ──
        //    Nhánh PersonalNeed (Carry) vốn đã 100% cá nhân nên không trộn thêm.
        decimal blended = gapScore;
        decimal? personalScore = null;

        if (ruleScoreVector is { } r && destiny is { } mine)
        {
            personalScore = Math.Clamp(r.Dot(product.Vector), -1m, 1m);
            blended = (1m - ctx.PersonalWeight) * gapScore + ctx.PersonalWeight * personalScore.Value;
            DescribePersonalAffinity(mine, productDominant, personalScore.Value, facts, cautions);
        }

        // ── Bước 3 — Directional Validation (bỏ qua với vật mang theo người / cây) ──
        var (dirPenalty, placementHint) = policy.Direction == DirectionMode.Soft
            ? ValidateDirection(ctx, productDominant)
            : (0m, PlacementHintWithoutDirection(product.Placement));

        decimal rawScore = blended - userPenalty - dirPenalty - vibePenalty;
        decimal clamped = Math.Clamp(rawScore, -1m, 1m);
        decimal score = Math.Round(clamped, 3);

        // Lưới an toàn: loại theo ĐIỂM TỔNG thay vì theo từng thuộc tính đơn lẻ. Fit không cắt —
        // trang chi tiết sản phẩm luôn phải trả về kết quả kể cả khi điểm rất thấp.
        if (mode == ScoreMode.Rank && score < ctx.Params.MinScoreThreshold)
            return null;

        var breakdown = new ScoreBreakdown(
            FormulaVersion: ScoringFormulaVersions.Current,
            Target: policy.Target,
            Placement: product.Placement,
            GapScore: gapScore,
            PersonalScore: personalScore,
            PersonalWeight: policy.Target == ScoringTarget.PersonalNeed ? 0m : ctx.PersonalWeight,
            PersonalWeightCode: policy.Target == ScoringTarget.PersonalNeed ? null : ScoringParamCodes.PersonalWeightFor(ctx.Scope),
            Blended: blended,
            UserPenalty: userPenalty,
            DirectionPenalty: dirPenalty,
            VibePenalty: vibePenalty,
            RawScore: rawScore,
            Clamped: clamped != rawScore,
            ProductVector: product.Vector,
            NormalizedGap: normalizedGap,
            RuleScoreVector: ruleScoreVector,
            CombinedDirection: combinedDirection,
            PersonalNeedVector: policy.Target == ScoringTarget.PersonalNeed ? ctx.PersonalNeedVector : null,
            PersonalVector: policy.Target == ScoringTarget.PersonalNeed ? null : ctx.PersonalVector,
            PersonalTarget: policy.Target == ScoringTarget.PersonalNeed || ctx.PersonalVector is not { } mine2
                ? null
                : ElementDirection.PersonalTargetOf(ctx.AdjustedIdeal, mine2, ctx.PersonalWeight),
            Components: BuildComponents(ctx, policy, product, normalizedGap, gapScore, personalScore, destiny),
            Penalties: BuildPenalties(
                ctx, userPenalty, userPenaltyCode, userPenaltyLabel, userPenaltyReason,
                dirPenalty, placementHint, vibePenalty, vibeCode, vibeReason),
            ConflictResolution: direction.ConflictResolution,
            DestinyElement: destiny,
            // Chỉ khai báo nghề nghiệp khi nó THẬT SỰ dịch được `r`. Trả mã nghề kèm một shift toàn 0
            // sẽ đẻ ra một lớp radar phẳng lì và một dòng breakdown vô nghĩa.
            BaseRuleScoreVector: direction.OccupationShift is null ? null : direction.BaseRuleScoreVector,
            OccupationCode: direction.OccupationShift is null ? null : ctx.OccupationCode,
            OccupationNameVi: direction.OccupationShift is null ? null : ctx.OccupationNameVi,
            OccupationShare: direction.OccupationShift is null ? 0m : ctx.Params.OccupationShare);

        return new ScoredProduct(product.ProductId, score, facts, cautions, placementHint, breakdown);
    }

    /// <summary>
    /// Các số hạng CỘNG vào điểm, đúng thứ tự waterfall của FE. Σ <c>Contribution</c> = <c>Blended</c>.
    /// </summary>
    private static IReadOnlyList<ScoreComponent> BuildComponents(
        ScoringContext ctx, PlacementPolicy policy, ProductFacts product,
        ElementVector normalizedGap, decimal gapScore, decimal? personalScore, FengShuiElement? destiny)
    {
        if (policy.Target == ScoringTarget.PersonalNeed)
        {
            // Carry không có phòng ⇒ waterfall chỉ MỘT thành phần. FE phải dùng component khác, không
            // dùng chung với luồng workspace (§PHẦN E #6).
            return new[]
            {
                new ScoreComponent(
                    ScoreComponentCodes.PersonalNeedScore, "Hợp dụng thần của bạn",
                    Value: gapScore, Weight: 1m, Contribution: gapScore,
                    ReasonVi: DescribeVectorMatch(
                        normalizedGap, product.Vector, "hành bạn đang cần được bồi", "hành chưa phải thứ bạn cần")),
            };
        }

        decimal wp = personalScore is null ? 0m : ctx.PersonalWeight;
        var components = new List<ScoreComponent>
        {
            new(ScoreComponentCodes.GapScore, "Khớp nhu cầu của phòng",
                Value: gapScore, Weight: 1m - wp, Contribution: (1m - wp) * gapScore,
                ReasonVi: DescribeVectorMatch(
                    normalizedGap, product.Vector, "hành phòng đang thiếu", "hành phòng đã thừa")),
        };

        if (personalScore is { } ps && destiny is { } mine)
        {
            var relation = FengShuiCalculator.GetRelation(mine, product.Vector.Dominant());
            components.Add(new ScoreComponent(
                ScoreComponentCodes.PersonalScore, "Hợp bản mệnh của bạn",
                Value: ps, Weight: wp, Contribution: wp * ps,
                ReasonVi: $"Hành trội {product.Vector.Dominant()} của sản phẩm {RelationVi(relation)} "
                    + $"bản mệnh {mine} của bạn."));
        }

        return components;
    }

    /// <summary>
    /// Mọi loại penalty, KỂ CẢ loại không bị áp. "Đã xét và không trừ" khác hẳn "không tồn tại" —
    /// accordion của FE liệt kê đủ để user không nghi ngờ có luật ẩn (§P4.6).
    /// </summary>
    private static IReadOnlyList<ScorePenalty> BuildPenalties(
        ScoringContext ctx,
        decimal userPenalty, string userPenaltyCode, string userPenaltyLabel, string userPenaltyReason,
        decimal dirPenalty, string? placementHint,
        decimal vibePenalty, string vibeCode, string vibeReason)
        => new[]
        {
            new ScorePenalty(userPenaltyCode, userPenaltyLabel,
                userPenalty, userPenalty > 0m, userPenaltyReason),

            new ScorePenalty(ScoringParamCodes.DirectionPenalty, "Hướng hợp bị chắn",
                dirPenalty, dirPenalty > 0m,
                dirPenalty > 0m
                    ? "Mọi hướng hợp với vật phẩm đều trùng cửa vào, nhà vệ sinh hoặc góc tối."
                    : placementHint ?? "Còn hướng hợp để đặt vật phẩm - không trừ điểm."),

            new ScorePenalty(vibeCode, "Lệch cảm hứng không gian", vibePenalty, vibePenalty > 0m, vibeReason),
        };

    /// <summary>Câu giải thích chung cho "vector mục tiêu × vector sản phẩm": nêu đúng hành đã khớp.</summary>
    private static string DescribeVectorMatch(
        ElementVector direction, ElementVector productVector, string wantedLabel, string unwantedLabel)
    {
        var hit = direction.Enumerate()
            .Where(x => x.Value > 0m && productVector[x.Element] > 0m)
            .OrderByDescending(x => x.Value * productVector[x.Element])
            .Take(2).Select(x => $"{x.Element} ({x.Value:+0.00;-0.00})").ToList();

        if (hit.Count > 0)
            return $"Sản phẩm cấp {string.Join(" và ", hit)} - đúng {wantedLabel}.";

        var miss = direction.Enumerate()
            .Where(x => x.Value < 0m && productVector[x.Element] > 0m)
            .OrderBy(x => x.Value)
            .Take(2).Select(x => $"{x.Element} ({x.Value:+0.00;-0.00})").ToList();

        return miss.Count > 0
            ? $"Sản phẩm cấp {string.Join(" và ", miss)} - {unwantedLabel}."
            : "Hành của sản phẩm trung tính với nhu cầu đang xét.";
    }

    private static string RelationVi(FengShuiRelation relation) => relation switch
    {
        FengShuiRelation.TuongHoa => "tỷ hòa với",
        FengShuiRelation.TuongSinh => "tương sinh, nuôi dưỡng",
        FengShuiRelation.TietKhi => "làm hao khí",
        FengShuiRelation.TuongKhac => "bị khắc chế bởi",
        _ => "khắc",
    };

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
                ? $"Hành {productDominant} khắc bản mệnh {personalDominant} - đã trừ vào điểm hợp mệnh."
                : $"Hành {productDominant} làm hao khí bản mệnh {personalDominant} - trừ nhẹ điểm hợp mệnh.");
        }
    }

    /// <summary>Gợi ý vị trí cho nhóm không xét hướng la bàn — thay chỗ của Directional Validation.</summary>
    private static string? PlacementHintWithoutDirection(ProductPlacement placement) => placement switch
    {
        ProductPlacement.Carry =>
            "Vật phẩm mang theo người - tác dụng đi theo bản mệnh của bạn, không phụ thuộc hướng đặt trong phòng.",
        ProductPlacement.Living =>
            "Cây/vật sống - ưu tiên vị trí đủ ánh sáng và dễ chăm sóc thay vì chọn theo hướng la bàn.",
        _ => null,
    };

    /// <summary>Caution cho trang Fit khi sản phẩm vốn không phải đồ đặt trong không gian.</summary>
    private static void DescribePlacementForFit(ProductPlacement placement, List<string> cautions)
    {
        switch (placement)
        {
            case ProductPlacement.Carry:
                cautions.Add("Đây là vật phẩm mang theo người - điểm dưới đây chấm theo nhu cầu của phòng, "
                    + "để chọn đúng nên xem gợi ý theo bản mệnh.");
                break;
            case ProductPlacement.Consumable:
                cautions.Add("Đây là hàng tiêu hao, cần thay định kỳ - không được engine đưa vào danh sách gợi ý.");
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
                    ? $"Mang hành {string.Join("/", bumped)} - đúng hành bản mệnh bạn cần được bồi."
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
                cautions.Add($"Bơm thêm hành {string.Join("/", worsened)} vốn đã thừa trong phòng - nên cân nhắc.");
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
                    + $"- cung {preferred.CungName} của bạn, đúng mục tiêu đang hướng tới.");

            return (0m, $"Hãy đặt vật phẩm này ở hướng {DirectionVi(placementDirs[0])} của phòng để kích hoạt năng lượng tốt nhất.");
        }

        return (ctx.Params.DirectionPenalty,
            "Các hướng hợp với vật phẩm đều bị chắn (cửa/WC/góc tối) - cân nhắc vị trí đặt.");
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

    /// <summary>
    /// Tổng tỉ trọng những hành KHẮC bản mệnh trong vector sản phẩm.
    ///
    /// <para>
    /// Đọc quan hệ từ <see cref="FengShuiCalculator.GetRelation"/> chứ không từ bảng
    /// <c>feng_shui_rules</c>: "khắc bản mệnh" là một PHẠM TRÙ của ngũ hành, không phải con số admin
    /// chỉnh được (§14.4). Điểm số mềm mới đi đường <c>r</c>.
    /// </para>
    /// </summary>
    private static decimal ClashShare(FengShuiElement destiny, ElementVector productVector)
    {
        decimal sum = 0m;
        foreach (var (element, value) in productVector.Enumerate())
            if (value > 0m && FengShuiCalculator.GetRelation(destiny, element) == FengShuiRelation.BiKhac)
                sum += value;
        return sum;
    }

    /// <summary>Tên tiếng Việt của các hành khắc mệnh đang có mặt — cho câu giải thích.</summary>
    private static string ClashingElementsVi(FengShuiElement destiny, ElementVector productVector)
        => string.Join(", ", productVector.Enumerate()
            .Where(x => x.Value > 0m
                        && FengShuiCalculator.GetRelation(destiny, x.Element) == FengShuiRelation.BiKhac)
            .OrderByDescending(x => x.Value)
            .Select(x => ElementSemantics.ElementName(x.Element)));

    /// <summary>Ủy quyền về <see cref="FengShuiCalculator.DirectionVi"/> — giữ một nguồn tên hướng duy nhất.</summary>
    private static string DirectionVi(CompassDirection d) => FengShuiCalculator.DirectionVi(d);
}
