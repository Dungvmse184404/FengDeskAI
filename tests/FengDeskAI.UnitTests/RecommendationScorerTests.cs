using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Đợt 7 — engine chấm điểm gợi ý (v3 + trục cá nhân v3.1). Data-Driven: mỗi ca là một
/// <see cref="ScoreCase"/> trong <see cref="RecommendationScorerTests.RankCases"/>, chạy qua đúng một
/// hàm kiểm chứng — thêm ca = thêm một dòng dữ liệu, không thêm hàm test.
///
/// <para>
/// Engine deterministic nên test khẳng định <b>GIÁ TRỊ CHÍNH XÁC</b>, không chỉ "không nổ". Bộ số
/// <see cref="Fx.Ideal"/>/<see cref="Fx.Current"/> được chọn để mọi kỳ vọng ra số tròn — xem bảng ở
/// <see cref="Fx"/>. Giá trị kỳ vọng tính TAY từ công thức trong ADR, không lấy từ code: lấy từ code
/// thì test chỉ chép lại lỗi.
/// </para>
///
/// Ca chia theo Normal / Boundary / Abnormal đúng phân loại N/B/A của Report5_Unit Test.xls.
/// Công thức đang khẳng định (<c>docs/adr/personalized-recommendation-v3.1.md</c> §3):
/// <code>
/// gap        = adjustedIdeal − current
/// gapScore   = gap · productVector / |gap|₁
/// personal   = Σ productVector[e] × ruleScore(mệnh, e)      (có dấu: +1.0 … −1.0)
/// blended    = (1 − Wp)·gapScore + Wp·personal              (chỉ nhánh WorkspaceGap)
/// score      = round(clamp(blended − userPenalty − dirPenalty − vibePenalty, −1, 1), 3)
/// </code>
/// </summary>
public sealed class RecommendationScorerTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ===================== Bộ chạy chung =====================

    private static ScoringContext ContextOf(ScoreCase c) => new()
    {
        AdjustedIdeal = c.Ideal,
        CurrentVector = c.Current,
        PersonalVector = c.Personal is { } mine ? ElementVector.Single(mine) : null,
        PersonalNeedVector = c.PersonalNeed,
        Scope = c.Scope,
        Purpose = c.Purpose,
        ViolatedDirections = c.Violated.ToHashSet(),
        PersonalWeight = c.Wp,
        Params = ScoringParameters.Default with
        {
            VibeFilterHard = c.VibeFilterHard,
            MinScoreThreshold = c.MinScoreThreshold,
        },
    };

    private static ProductFacts FactsOf(ScoreCase c)
        => new(ProductId, c.Product, c.Vibes.ToHashSet(), c.Placement);

    private static ScoredProduct? RunRank(ScoreCase c)
        => new RecommendationScorer().Score(ContextOf(c), new[] { FactsOf(c) }).FirstOrDefault();

    // ===================== A. Engine — bảng ca dữ liệu =====================

    [Theory(DisplayName = "SCORE")]
    [MemberData(nameof(RankCases))]
    public void Score_ProducesExpectedScore(ScoreCase c)
    {
        var actual = RunRank(c);

        if (c.Expected is null)
        {
            Assert.True(actual is null, $"{c.Id}: kỳ vọng BỊ LOẠI khỏi danh sách. {c.Why}");
            return;
        }

        Assert.True(actual is not null, $"{c.Id}: kỳ vọng còn trong danh sách với điểm {c.Expected}. {c.Why}");
        Assert.Equal(c.Expected.Value, actual!.Score);
    }

    public static TheoryData<ScoreCase> RankCases()
    {
        var data = new TheoryData<ScoreCase>();

        // ── A1. Trục cá nhân × trọng số Wp — lõi v3.1 ──
        // score = (1−Wp)·gapScore + Wp·personalScore. Mệnh user = Mộc ở toàn nhóm.
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-01", Name = "[Normal] Wood item in a private room blends room gap with destiny",
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = 0.650m,
            Why = "0.5×(+0.300) + 0.5×(+1.000) — hợp cả phòng lẫn mệnh.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-02", Name = "[Abnormal] Metal item clashing with a Wood destiny is ranked last, not dropped",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.625m,
            Why = "0.5×(−0.250) + 0.5×(−1.000). v3.1 bỏ hard-filter khắc mệnh — xung khắc đã có dấu trong personalScore.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-03", Name = "[Normal] Water item nourishes a Wood destiny",
            Product = Fx.Of(FengShuiElement.Thuy), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = 0.500m,
            Why = "0.5×(+0.200) + 0.5×(+0.800) — Thủy sinh Mộc.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-04", Name = "[Normal] A shared room weights destiny at 0.3 instead of 0.5",
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = 0.510m,
            Why = "0.7×(+0.300) + 0.3×(+1.000).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-05", Name = "[Abnormal] A shared room softens the clash penalty",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = -0.475m,
            Why = "0.7×(−0.250) + 0.3×(−1.000) — nhẹ hơn phòng riêng (−0.625).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-06", Name = "[Boundary] A public room ignores destiny entirely",
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Public, Wp = 0.00m, Expected = 0.300m,
            Why = "Wp = 0 ⇒ score = gapScore. Không gian chung không được neo vào bản mệnh một người.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-07", Name = "[Normal] A destiny-controlled element cannot rescue a negative room gap",
            Product = Fx.Of(FengShuiElement.Tho), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.025m,
            Why = "0.5×(−0.250) + 0.5×(+0.200) — Mộc khắc Thổ chỉ +0.2, không đủ bù gap âm.",
        });

        // ── A2. Kill-switch: PERSONAL_WEIGHT_* = 0 phải cho ra ĐÚNG hành vi v3 ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-01", Name = "[Boundary] With the personal axis off the v3 hard filter comes back",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = null,
            Why = "Wp = 0 ⇒ PersonalConflictMode.ByScope + Private ⇒ loại cứng như trước v3.1.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-02", Name = "[Boundary] With the personal axis off the score is byte-identical to v3",
            Product = Fx.Of(FengShuiElement.Tho), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = -0.250m,
            Why = "Chỉ còn gapScore — hợp đồng 'merge không đổi ranking' của kill-switch.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-03", Name = "[Abnormal] A user with no date of birth falls back to the room-only score",
            Product = Fx.Of(FengShuiElement.Kim), Personal = null,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.250m,
            Why = "PersonalBlendActive cần CẢ Wp > 0 lẫn PersonalVector — thiếu ngày sinh thì không trộn, cũng không loại.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-04", Name = "[Boundary] The clash is never charged twice",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = -0.475m,
            Why = "Trục cá nhân bật ⇒ USER_CONFLICT_PENALTY (0.30) KHÔNG được trừ thêm; nếu trừ sẽ là −0.775.",
        });

        // ── A3. Biên số học ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A3-01", Name = "[Boundary] A perfectly balanced room scores zero without dividing by zero",
            Ideal = Fx.Current, Current = Fx.Current,
            Product = Fx.Of(FengShuiElement.Moc), Expected = 0.000m,
            Why = "|gap|₁ = 0 ⇒ gapScore = 0 (không DivideByZeroException).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A3-02", Name = "[Boundary] A product with no declared elements scores zero",
            Product = ElementVector.Zero, Expected = 0.000m,
            Why = "Tích vô hướng = 0. Dominant() của vector rỗng rơi về Thổ, vẫn còn hướng hợp nên không bị phạt hướng.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A3-03", Name = "[Boundary] The score is clamped at minus one",
            Ideal = Fx.Of(FengShuiElement.Moc), Current = Fx.Of(FengShuiElement.Kim),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m,
            Purpose = WorkPurpose.Office, Vibes = new[] { "Relax" }, VibeFilterHard = 0.00m,
            Violated = Enum.GetValues<CompassDirection>(), Expected = -1.000m,
            Why = "0.5×(−0.5) + 0.5×(−1.0) − 0.15 (hướng) − 0.20 (vibe) = −1.10 ⇒ clamp về −1.000.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A3-04", Name = "[Boundary] The score is clamped at plus one",
            Ideal = Fx.Of(FengShuiElement.Moc), Current = Fx.Of(FengShuiElement.Kim),
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 1.00m, Expected = 1.000m,
            Why = "Wp = 1 ⇒ blended = personalScore = +1.0; không vượt trần.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A3-05", Name = "[Boundary] A repeating decimal is rounded to three places",
            Product = Fx.Mix((FengShuiElement.Moc, 1m), (FengShuiElement.Thuy, 1m), (FengShuiElement.Hoa, 1m)),
            Expected = 0.167m,
            Why = "(0.6 + 0.4)×⅓ / 2 = 0.16666… ⇒ 0.167 (Math.Round 3 chữ số).",
        });

        // ── A4. PlacementPolicy — 4 giá trị ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-01", Name = "[Abnormal] Consumables never enter a recommendation list",
            Placement = ProductPlacement.Consumable, Expected = null,
            Why = "IsRecommendable = false — nhang/nến/muối vẫn tìm & mua được nhưng không được gợi ý.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-02", Name = "[Boundary] Living items are not penalised even when every direction is blocked",
            Placement = ProductPlacement.Living, Violated = Enum.GetValues<CompassDirection>(),
            Expected = 0.300m,
            Why = "DirectionMode.None — cây đặt theo ánh sáng, không theo la bàn.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-03", Name = "[Abnormal] Desk items lose DIRECTION_PENALTY when every fitting direction is blocked",
            Placement = ProductPlacement.Desk,
            Violated = new[] { CompassDirection.East, CompassDirection.Southeast, CompassDirection.North },
            Expected = 0.150m,
            Why = "Hướng hợp Mộc = Đông/Đông Nam ∪ hướng của Thủy (mẹ) = Bắc. Chắn hết ⇒ 0.300 − 0.150.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-04", Name = "[Normal] Carry items score against the person, ignoring the room and Wp",
            Placement = ProductPlacement.Carry, PersonalNeed = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Purpose = WorkPurpose.Office,
            Expected = 1.000m,
            Why = "Target = PersonalNeedVector ⇒ 1.0. Không trộn Wp (vốn đã 100% cá nhân), không lọc vibe phòng.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-05", Name = "[Abnormal] A clashing carry item is dropped even in a public space",
            Placement = ProductPlacement.Carry, PersonalNeed = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Public, Wp = 0.50m, Expected = null,
            Why = "PersonalConflictMode.AlwaysHard — vật đeo trên người là riêng tư tuyệt đối, scope không cứu được.",
        });

        // ── A5. Vibe: cứng (v3) ↔ mềm (kill-switch VIBE_FILTER_HARD) ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-01", Name = "[Abnormal] A vibe mismatch is a hard filter while VIBE_FILTER_HARD is on",
            Purpose = WorkPurpose.Office, Vibes = new[] { "Relax" }, VibeFilterHard = 1.00m,
            Expected = null,
            Why = "Phòng Office cần vibe Focus. Hành vi v3 gốc: loại khỏi candidates.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-02", Name = "[Normal] A vibe mismatch becomes VIBE_MISMATCH_PENALTY when the filter is soft",
            Purpose = WorkPurpose.Office, Vibes = new[] { "Relax" }, VibeFilterHard = 0.00m,
            Expected = 0.100m,
            Why = "0.300 − 0.200.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-03", Name = "[Boundary] An undeclared vibe costs less than a mismatched one",
            Purpose = WorkPurpose.Office, Vibes = Array.Empty<string>(), VibeFilterHard = 0.00m,
            Expected = 0.250m,
            Why = "0.300 − 0.050. Thiếu dữ liệu KHÁC bằng chứng lệch mục đích.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-04", Name = "[Normal] A matching vibe is never penalised",
            Purpose = WorkPurpose.Office, Vibes = new[] { "Focus" }, VibeFilterHard = 1.00m,
            Expected = 0.300m,
            Why = "Khớp vibe mục đích ⇒ không phạt, không loại.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-05", Name = "[Boundary] WorkPurpose.Other disables vibe filtering altogether",
            Purpose = WorkPurpose.Other, Vibes = new[] { "Relax" }, VibeFilterHard = 1.00m,
            Expected = 0.300m,
            Why = "TargetVibe(Other) = null ⇒ không lọc, không phạt.",
        });

        // ── A6. MIN_SCORE_THRESHOLD — lưới an toàn theo ĐIỂM TỔNG ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A6-01", Name = "[Normal] The default threshold keeps negative scores in the list",
            Product = Fx.Of(FengShuiElement.Tho), MinScoreThreshold = -1.00m, Expected = -0.250m,
            Why = "Mặc định −1.0 = không cắt (điểm đã clamp trong [−1, 1]).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A6-02", Name = "[Boundary] Raising the threshold to zero cuts negative scores",
            Product = Fx.Of(FengShuiElement.Tho), MinScoreThreshold = 0.00m, Expected = null,
            Why = "−0.250 < 0 ⇒ loại.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A6-03", Name = "[Boundary] A score exactly on the threshold survives",
            Product = Fx.Of(FengShuiElement.Hoa), MinScoreThreshold = 0.00m, Expected = 0.000m,
            Why = "Điều kiện loại là score < threshold, KHÔNG phải <=.",
        });

        return data;
    }

    // ===================== A3-06. Bất biến của gapScore =====================

    /// <summary>
    /// Với hai vector trạng thái đã chuẩn hóa (Σ = 1), <c>gapScore</c> luôn nằm trong [−0.5, +0.5]:
    /// tổng phần dương và tổng phần âm của gap đều bằng <c>|gap|₁ / 2</c>. Đây là chốt chặn — ai sửa
    /// công thức mà làm vỡ biên này sẽ khiến trọng số Wp không còn so sánh được giữa hai trục.
    /// </summary>
    [Fact(DisplayName = "SCORE-A3-06 [Boundary] gapScore stays within plus or minus one half for every element pair")]
    public void GapScore_ForNormalisedVectors_StaysWithinHalf()
    {
        var elements = Enum.GetValues<FengShuiElement>();
        var scorer = new RecommendationScorer();

        foreach (var ideal in elements)
        foreach (var current in elements)
        foreach (var product in elements)
        {
            var ctx = new ScoringContext
            {
                AdjustedIdeal = ElementVector.Single(ideal),
                CurrentVector = ElementVector.Single(current),
                Scope = WorkspaceScope.Public,
                Purpose = WorkPurpose.Other,
                Params = ScoringParameters.Default,
            };
            // Living: bỏ hẳn Directional Validation nên điểm còn lại ĐÚNG bằng gapScore.
            var facts = new ProductFacts(ProductId, ElementVector.Single(product),
                new HashSet<string>(), ProductPlacement.Living);

            var scored = scorer.Score(ctx, new[] { facts }).Single();

            Assert.InRange(scored.Score, -0.5m, 0.5m);
        }
    }

    // ===================== A4-06 + Fit: trang chi tiết sản phẩm không bao giờ loại =====================

    [Theory(DisplayName = "SCORE-FIT")]
    [MemberData(nameof(FitCases))]
    public void ScoreSingle_NeverDrops(ScoreCase c)
    {
        var scored = new RecommendationScorer().ScoreSingle(ContextOf(c), FactsOf(c));

        Assert.NotNull(scored);
        if (c.Expected is { } expected)
            Assert.Equal(expected, scored.Score);
    }

    public static TheoryData<ScoreCase> FitCases()
    {
        var data = new TheoryData<ScoreCase>();

        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-06", Name = "[Abnormal] Fit mode still scores a consumable instead of dropping it",
            Placement = ProductPlacement.Consumable, Expected = 0.300m,
            Why = "Hợp đồng của ScoreSingle: trang chi tiết sản phẩm LUÔN có kết quả, placement chỉ sinh caution.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-07", Name = "[Abnormal] Fit mode scores a carry item against the room and warns about it",
            Placement = ProductPlacement.Carry, Expected = 0.300m,
            Why = "Fit luôn dùng PlacementPolicy.WorkspaceFit ⇒ chấm theo gap phòng + caution 'nên xem gợi ý theo bản mệnh'.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-06", Name = "[Abnormal] Fit mode keeps a vibe mismatch even with the hard filter on",
            Purpose = WorkPurpose.Office, Vibes = new[] { "Relax" }, VibeFilterHard = 1.00m,
            Expected = 0.300m,
            Why = "Loại cứng chỉ áp cho mode Rank; Fit vẫn trả điểm, chỉ thêm caution.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-05", Name = "[Abnormal] Fit mode charges USER_CONFLICT_PENALTY while the personal axis is off",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = -0.550m,
            Why = "−0.250 − 0.300. Đây là hành vi v3 mà kill-switch phải giữ nguyên.",
        });

        return data;
    }

    // ===================== Hợp đồng cấp danh sách =====================

    /// <summary>
    /// Lý do tồn tại của v3.1: hai người khác bản mệnh, cùng một phòng, cùng một catalog thì thứ hạng
    /// PHẢI khác nhau. Nếu ca này xanh khi <c>PERSONAL_WEIGHT_*</c> = 0 thì trục cá nhân đang không có
    /// tác dụng gì.
    /// </summary>
    [Fact(DisplayName = "SCORE-B-01 [Normal] Two users with different destinies get different rankings in the same room")]
    public void Score_TwoDestiniesSameRoom_ProducesDifferentRanking()
    {
        var scorer = new RecommendationScorer();
        var candidates = new[]
        {
            new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Moc), new HashSet<string>()),
            new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Kim), new HashSet<string>()),
            new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Thuy), new HashSet<string>()),
            new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Tho), new HashSet<string>()),
        };

        ScoringContext For(FengShuiElement destiny) => new()
        {
            AdjustedIdeal = Fx.Ideal,
            CurrentVector = Fx.Current,
            PersonalVector = ElementVector.Single(destiny),
            Scope = WorkspaceScope.Private,
            Purpose = WorkPurpose.Other,
            PersonalWeight = 0.50m,
            Params = ScoringParameters.Default,
        };

        var wood = scorer.Score(For(FengShuiElement.Moc), candidates);
        var metal = scorer.Score(For(FengShuiElement.Kim), candidates);

        Assert.Equal(candidates[0].ProductId, wood[0].ProductId);   // Mộc: 0.650
        Assert.Equal(candidates[1].ProductId, metal[0].ProductId);  // Kim: 0.375
        Assert.NotEqual(wood[0].ProductId, metal[0].ProductId);
    }

    /// <summary>Ngưỡng cắt hết catalog thì trả danh sách rỗng, không ném.</summary>
    [Fact(DisplayName = "SCORE-A6-04 [Abnormal] A threshold above every candidate returns an empty list")]
    public void Score_ThresholdAboveEveryCandidate_ReturnsEmpty()
    {
        var ctx = new ScoringContext
        {
            AdjustedIdeal = Fx.Ideal,
            CurrentVector = Fx.Current,
            Scope = WorkspaceScope.Public,
            Purpose = WorkPurpose.Other,
            Params = ScoringParameters.Default with { MinScoreThreshold = 0.00m },
        };
        var candidates = new[]
        {
            new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Tho), new HashSet<string>()),
            new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Kim), new HashSet<string>()),
        };

        Assert.Empty(new RecommendationScorer().Score(ctx, candidates));
    }

    /// <summary>Danh sách trả về phải giảm dần theo điểm — FE và AI đều dựa vào thứ tự này.</summary>
    [Fact(DisplayName = "SCORE-B-02 [Normal] Results are ordered by descending score")]
    public void Score_Results_AreOrderedByScoreDescending()
    {
        var ctx = new ScoringContext
        {
            AdjustedIdeal = Fx.Ideal,
            CurrentVector = Fx.Current,
            Scope = WorkspaceScope.Public,
            Purpose = WorkPurpose.Other,
            Params = ScoringParameters.Default,
        };
        var candidates = Enum.GetValues<FengShuiElement>()
            .Select(e => new ProductFacts(Guid.NewGuid(), Fx.Of(e), new HashSet<string>()))
            .ToList();

        var scored = new RecommendationScorer().Score(ctx, candidates);

        Assert.Equal(scored.OrderByDescending(s => s.Score).Select(s => s.ProductId), scored.Select(s => s.ProductId));
    }
}

/// <summary>
/// Bộ số cố định của mọi ca tầng A. Chọn để KỲ VỌNG RA SỐ TRÒN, tính tay được trên giấy:
/// <code>
/// adjustedIdeal = { Mộc 0.6, Thủy 0.4 }        current = { Kim 0.5, Thổ 0.5 }
/// gap           = Mộc +0.6, Thủy +0.4, Kim −0.5, Thổ −0.5          |gap|₁ = 2.0
///
/// SP thuần      gapScore    personalScore (mệnh Mộc)
/// Mộc            +0.300      +1.0   tỷ hòa
/// Thủy           +0.200      +0.8   Thủy sinh Mộc
/// Hỏa             0.000      −0.2   Mộc sinh Hỏa (tiết khí)
/// Thổ            −0.250      +0.2   Mộc khắc Thổ
/// Kim            −0.250      −1.0   Kim khắc Mộc
/// </code>
/// </summary>
public static class Fx
{
    public static readonly ElementVector Ideal = new(Tho: 0m, Kim: 0m, Thuy: 0.4m, Moc: 0.6m, Hoa: 0m);
    public static readonly ElementVector Current = new(Tho: 0.5m, Kim: 0.5m, Thuy: 0m, Moc: 0m, Hoa: 0m);

    public static ElementVector Of(FengShuiElement e) => ElementVector.Single(e);

    public static ElementVector Mix(params (FengShuiElement Element, decimal Weight)[] parts)
        => ElementVector.FromContributions(
            parts.Select(p => new KeyValuePair<FengShuiElement, decimal>(p.Element, p.Weight)));
}

/// <summary>
/// Một ca chấm điểm. Mặc định = bộ <see cref="Fx"/> + sản phẩm thuần Mộc + phòng riêng + không mục
/// đích (không lọc vibe) + không hướng bị chắn — mỗi ca chỉ khai ĐÚNG những gì nó thay đổi, nên đọc
/// một dòng là biết ca đó đang thử biến nào.
/// </summary>
public sealed class ScoreCase
{
    /// <summary>Mã ca, khớp cột ID của Report5_Unit Test.xls.</summary>
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    public ElementVector Ideal { get; init; } = Fx.Ideal;
    public ElementVector Current { get; init; } = Fx.Current;
    public ElementVector Product { get; init; } = Fx.Of(FengShuiElement.Moc);

    /// <summary>Bản mệnh user. Null = chưa có ngày sinh ⇒ trục cá nhân tắt.</summary>
    public FengShuiElement? Personal { get; init; }

    /// <summary>Vector dụng thần — chỉ nhánh <see cref="ProductPlacement.Carry"/> đọc tới.</summary>
    public ElementVector? PersonalNeed { get; init; }

    public WorkspaceScope Scope { get; init; } = WorkspaceScope.Private;

    /// <summary>PERSONAL_WEIGHT_* đã resolve theo scope (RecommendationService làm việc này).</summary>
    public decimal Wp { get; init; }

    public ProductPlacement Placement { get; init; } = ProductPlacement.Desk;

    /// <summary><see cref="WorkPurpose.Other"/> = không có vibe mục tiêu ⇒ tắt hẳn nhánh lọc vibe.</summary>
    public WorkPurpose Purpose { get; init; } = WorkPurpose.Other;

    public string[] Vibes { get; init; } = Array.Empty<string>();
    public CompassDirection[] Violated { get; init; } = Array.Empty<CompassDirection>();

    public decimal VibeFilterHard { get; init; } = 1.00m;
    public decimal MinScoreThreshold { get; init; } = -1.00m;

    /// <summary>Điểm kỳ vọng; <c>null</c> = kỳ vọng BỊ LOẠI khỏi danh sách.</summary>
    public decimal? Expected { get; init; }

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
