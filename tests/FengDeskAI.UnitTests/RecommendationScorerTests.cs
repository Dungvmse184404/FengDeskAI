using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Đợt 7 — engine chấm điểm gợi ý (v3 + trục cá nhân v3.1 + chuẩn hoá thang điểm & L2 của v3.2).
/// Data-Driven: mỗi ca là một <see cref="ScoreCase"/>, chạy qua đúng một hàm kiểm chứng — thêm ca =
/// thêm một dòng dữ liệu, không thêm hàm test.
///
/// <para>
/// Engine deterministic nên test khẳng định <b>GIÁ TRỊ CHÍNH XÁC</b>. Bộ số <see cref="Fx"/> chọn để
/// mọi kỳ vọng ra số tròn; giá trị kỳ vọng tính TAY từ công thức trong ADR, không lấy từ code.
/// </para>
///
/// <para>Bộ test này khoá 4 thay đổi của v3.2 — xem <c>docs/adr/score-explainability-v3.2.md</c>
/// §8 và §14:</para>
/// <list type="number">
/// <item><b>Chuẩn hoá gapScore</b> — nhánh <see cref="ScoringTarget.WorkspaceGap"/> chia cho
/// <c>|gap|₁ / 2</c> thay vì <c>|gap|₁</c>. Nhánh <see cref="ScoringTarget.PersonalNeed"/> GIỮ NGUYÊN
/// <c>|target|₁</c> — target ở đó là vector Σ=1 không âm, không có "hai nửa" để chia đôi.</item>
/// <item><b>4 penalty nhân đôi</b> trong <see cref="ScoringParameters"/> (0.60 / 0.30 / 0.40 / 0.10).</item>
/// <item><b>PersonalConflictMode.Scaled</b> — khi trục cá nhân bật, <c>BiKhac</c> trừ
/// <c>USER_CONFLICT_PENALTY × Wp</c> thay vì bỏ hẳn penalty.</item>
/// <item><b>PersonalConflictMode.None ở <see cref="WorkspaceScope.Public"/></b> — không gian chung
/// không lọc cũng không phạt theo bản mệnh của một người (§14.3 · Q12).</item>
/// </list>
///
/// Công thức đang khẳng định:
/// <code>
/// ĝ     = gap / (|gap|₁ / 2)                    // ∈ [−1, +1]  ← v3.2
/// r[e]  = ruleScore(mệnh, e)                    // ∈ [−1, +1], CÓ DẤU
/// d     = (1 − Wp)·ĝ + Wp·r
/// score = round( clamp( productVector·d − userPenalty − dirPenalty − vibePenalty, −1, 1 ), 3 )
/// </code>
///
/// Ca chia theo Normal / Boundary / Abnormal đúng phân loại N/B/A của Report5_Unit Test.xls.
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

    // ===================== Tham số phải khớp seed =====================

    /// <summary>
    /// Penalty là hằng số TUYỆT ĐỐI, nên khi miền <c>gapScore</c> nở từ ±0.5 lên ±1.0 thì sức nặng
    /// tương đối của chúng giảm một nửa. v3.2 nhân đôi cả 4 để giữ đúng tỉ lệ cũ. Default trong code
    /// phải khớp <c>seed-data/scoring-params.json</c> — lệch nhau là môi trường thiếu row sẽ chấm khác prod.
    /// </summary>
    [Fact(DisplayName = "SCORE-PARAM-01 [Normal] Penalty defaults match the doubled v3.2 values")]
    public void ScoringParameters_Defaults_MatchV32DoubledPenalties()
    {
        var p = ScoringParameters.Default;

        Assert.Equal(0.60m, p.UserConflictPenalty);
        Assert.Equal(0.30m, p.DirectionPenalty);
        Assert.Equal(0.40m, p.VibeMismatchPenalty);
        Assert.Equal(0.10m, p.VibeUnknownPenalty);
        Assert.Equal(-1.00m, p.MinScoreThreshold);

        // Trọng số trục cá nhân cũng phải khớp seed, và vì lý do NGƯỢC ĐỜI hơn nhóm penalty: DB thiếu
        // row thì rơi về default code, nên nếu hai chỗ lệch, seed xong tính năng lại TẮT đi thay vì bật
        // lên — đúng vết đã xảy ra khi default là 0.50 còn seed ghi 0.00.
        Assert.Equal(0.50m, p.PersonalWeightPrivate);
        Assert.Equal(0.30m, p.PersonalWeightShared);
        Assert.Equal(0.00m, p.PersonalWeightPublic);
    }

    /// <summary>
    /// Không có ngày sinh thì <c>Wp = 0</c> bất kể scope — không có bản mệnh thì không có gì để trộn.
    /// Điều kiện này từng nằm rải ở từng service; gom vào <see cref="ScoringParameters"/> rồi thì phải
    /// có ca khoá, nếu không một service mới lại quên và chấm bằng bản mệnh của người không khai.
    /// </summary>
    [Fact(DisplayName = "SCORE-PARAM-02 [Boundary] A user without a birth date always gets zero personal weight")]
    public void PersonalWeight_WithoutBirthDate_IsZeroInEveryScope()
    {
        var p = ScoringParameters.Default;

        foreach (var scope in Enum.GetValues<WorkspaceScope>())
        {
            Assert.Equal(0m, p.PersonalWeightFor(scope, null));
            Assert.Equal(p.PersonalWeightFor(scope), p.PersonalWeightFor(scope, new DateTime(1988, 5, 12)));
        }
    }

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

        // ── A1. Trục cá nhân × trọng số Wp — lõi v3.1, thang điểm v3.2 ──
        // gapScore thuần: Mộc +0.600 · Thủy +0.400 · Hỏa 0.000 · Thổ −0.500 · Kim −0.500
        // personalScore (mệnh Mộc): Mộc +1.0 · Thủy +0.8 · Hỏa −0.2 · Thổ +0.2 · Kim −1.0
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-01", Name = "[Normal] Wood item in a private room blends room gap with destiny",
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = 0.800m,
            Why = "0.5×(+0.600) + 0.5×(+1.000) = 0.800. Tỷ hòa ⇒ không có penalty L2.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-02", Name = "[Abnormal] Metal item clashing with a Wood destiny is ranked last, not dropped",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -1.000m,
            Why = "0.5×(−0.500) + 0.5×(−1.000) = −0.750; L2 trừ 0.60×0.50 = 0.300 ⇒ −1.050 ⇒ clamp −1.000. "
                + "Vẫn CÒN trong danh sách — v3.1 bỏ hard-filter.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-03", Name = "[Normal] Water item nourishes a Wood destiny",
            Product = Fx.Of(FengShuiElement.Thuy), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = 0.600m,
            Why = "0.5×(+0.400) + 0.5×(+0.800). Thủy sinh Mộc ⇒ không phải BiKhac, không penalty.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-04", Name = "[Normal] A shared room weights destiny at 0.3 instead of 0.5",
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = 0.720m,
            Why = "0.7×(+0.600) + 0.3×(+1.000).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-05", Name = "[Abnormal] A shared room softens both the blend and the clash penalty",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = -0.830m,
            Why = "0.7×(−0.500) + 0.3×(−1.000) = −0.650; L2 trừ 0.60×0.30 = 0.180 ⇒ −0.830.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-06", Name = "[Boundary] A public room ignores destiny entirely",
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Public, Wp = 0.00m, Expected = 0.600m,
            Why = "Wp = 0 ⇒ score = gapScore. Không gian chung không neo vào bản mệnh một người.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A1-07", Name = "[Normal] A destiny-controlled element is not penalised, only weakly helped",
            Product = Fx.Of(FengShuiElement.Tho), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.150m,
            Why = "0.5×(−0.500) + 0.5×(+0.200). Mộc khắc Thổ = TuongKhac, KHÔNG phải BiKhac ⇒ không penalty.",
        });

        // ── A2. Kill-switch: PERSONAL_WEIGHT_* = 0 phải cho ĐÚNG hành vi v3 ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-01", Name = "[Boundary] With the personal axis off the v3 hard filter comes back",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = null,
            Why = "Wp = 0 ⇒ PersonalConflictMode.ByScope + Private ⇒ loại cứng. Đứt gãy tại Wp=0 là CÓ CHỦ ĐÍCH (§14.3).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-02", Name = "[Boundary] With the personal axis off the score is pure room gap",
            Product = Fx.Of(FengShuiElement.Tho), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = -0.500m,
            Why = "Chỉ còn gapScore đã chuẩn hoá (−0.250 × 2 = −0.500).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-03", Name = "[Abnormal] A user with no date of birth falls back to the room-only score",
            Product = Fx.Of(FengShuiElement.Kim), Personal = null,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.500m,
            Why = "PersonalBlendActive cần CẢ Wp > 0 lẫn PersonalVector — thiếu ngày sinh thì không trộn, không loại, không phạt.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-04", Name = "[Boundary] The clash penalty scales with Wp instead of applying in full",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = -0.830m,
            Why = "L2: phạt 0.60×0.30 = 0.180. Nếu áp ĐỦ 0.60 sẽ ra −1.250 ⇒ clamp −1.000 — ca này phân biệt hai hành vi.",
        });

        // ── A3. Biên số học ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A3-01", Name = "[Boundary] A perfectly balanced room scores zero without dividing by zero",
            Ideal = Fx.Current, Current = Fx.Current,
            Product = Fx.Of(FengShuiElement.Moc), Expected = 0.000m,
            Why = "|gap|₁ = 0 ⇒ mẫu số 0 ⇒ gapScore = 0 (không DivideByZeroException).",
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
            Why = "0.5×(−1.0) + 0.5×(−1.0) = −1.000; − 0.300 (L2) − 0.300 (hướng) − 0.400 (vibe) = −2.000 ⇒ clamp −1.000.",
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
            Expected = 0.333m,
            Why = "(0.6 + 0.4)×⅓ / 1.0 = 0.3333… ⇒ 0.333 (Math.Round 3 chữ số).",
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
            Expected = 0.600m,
            Why = "DirectionMode.None — cây đặt theo ánh sáng, không theo la bàn.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-03", Name = "[Abnormal] Desk items lose DIRECTION_PENALTY when every fitting direction is blocked",
            Placement = ProductPlacement.Desk,
            Violated = new[] { CompassDirection.East, CompassDirection.Southeast, CompassDirection.North },
            Expected = 0.300m,
            Why = "Hướng hợp Mộc = Đông/Đông Nam ∪ hướng của Thủy (mẹ) = Bắc. Chắn hết ⇒ 0.600 − 0.300.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-04", Name = "[Normal] Carry items score against the person, ignoring the room and Wp",
            Placement = ProductPlacement.Carry, PersonalNeed = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Moc), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Purpose = WorkPurpose.Office,
            Expected = 1.000m,
            Why = "Target = PersonalNeedVector, mẫu số GIỮ |target|₁ = 1.0 (Σ=1 không âm, không có hai nửa để chia đôi) "
                + "⇒ 1.000. Không trộn Wp, không lọc vibe phòng.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-05", Name = "[Abnormal] A clashing carry item is dropped even in a public space",
            Placement = ProductPlacement.Carry, PersonalNeed = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Public, Wp = 0.50m, Expected = null,
            Why = "PersonalConflictMode.AlwaysHard — vật đeo trên người là riêng tư tuyệt đối, Scope và Wp không cứu được.",
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
            Expected = 0.200m,
            Why = "0.600 − 0.400.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-03", Name = "[Boundary] An undeclared vibe costs less than a mismatched one",
            Purpose = WorkPurpose.Office, Vibes = Array.Empty<string>(), VibeFilterHard = 0.00m,
            Expected = 0.500m,
            Why = "0.600 − 0.100. Thiếu dữ liệu KHÁC bằng chứng lệch mục đích — tỉ lệ 4:1 giữ nguyên sau khi nhân đôi.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-04", Name = "[Normal] A matching vibe is never penalised",
            Purpose = WorkPurpose.Office, Vibes = new[] { "Focus" }, VibeFilterHard = 1.00m,
            Expected = 0.600m,
            Why = "Khớp vibe mục đích ⇒ không phạt, không loại.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-05", Name = "[Boundary] WorkPurpose.Other disables vibe filtering altogether",
            Purpose = WorkPurpose.Other, Vibes = new[] { "Relax" }, VibeFilterHard = 1.00m,
            Expected = 0.600m,
            Why = "TargetVibe(Other) = null ⇒ không lọc, không phạt.",
        });

        // ── A6. MIN_SCORE_THRESHOLD — lưới an toàn theo ĐIỂM TỔNG ──
        data.Add(new ScoreCase
        {
            Id = "SCORE-A6-01", Name = "[Normal] The default threshold keeps negative scores in the list",
            Product = Fx.Of(FengShuiElement.Tho), MinScoreThreshold = -1.00m, Expected = -0.500m,
            Why = "Mặc định −1.0 = không cắt (điểm đã clamp trong [−1, 1]).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A6-02", Name = "[Boundary] Raising the threshold to zero cuts negative scores",
            Product = Fx.Of(FengShuiElement.Tho), MinScoreThreshold = 0.00m, Expected = null,
            Why = "−0.500 < 0 ⇒ loại.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A6-03", Name = "[Boundary] A score exactly on the threshold survives",
            Product = Fx.Of(FengShuiElement.Hoa), MinScoreThreshold = 0.00m, Expected = 0.000m,
            Why = "Điều kiện loại là score < threshold, KHÔNG phải <=.",
        });

        // ── L2. PersonalConflictMode.Scaled — phòng cần ĐÚNG hành khắc mệnh (v3.2 §14) ──
        // Bộ số riêng: ideal = {Kim 1.0}, current = {Mộc 1.0} ⇒ ĝ = {Kim +1.0, Mộc −1.0}; mệnh Mộc.
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-01", Name = "[Normal] A clashing element the room badly needs is pushed below neutral",
            Ideal = Fx.Of(FengShuiElement.Kim), Current = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.300m,
            Why = "0.5×(+1.0) + 0.5×(−1.0) = 0.000 — hai lực TRIỆT TIÊU. Không có L2 thì hiện 50% \"Trung tính\"; "
                + "L2 trừ 0.60×0.50 = 0.300 ⇒ −0.300 (35%, \"Cân nhắc\"). ĐÂY LÀ LÝ DO L2 TỒN TẠI.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-02", Name = "[Normal] In a shared room the room's need outweighs the clash",
            Ideal = Fx.Of(FengShuiElement.Kim), Current = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Shared, Wp = 0.30m, Expected = 0.220m,
            Why = "0.7×(+1.0) + 0.3×(−1.0) = 0.400; L2 trừ 0.60×0.30 = 0.180 ⇒ +0.220. "
                + "Phòng chung: nhu cầu phòng vẫn thắng — đúng thiết kế.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-03", Name = "[Boundary] At Wp zero the rule falls back to the v3 hard filter",
            Ideal = Fx.Of(FengShuiElement.Kim), Current = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = null,
            Why = "Đứt gãy CÓ CHỦ ĐÍCH: Wp = 0 nghĩa là tắt hẳn trục cá nhân v3.1, rơi trọn về luật v3 (§14.3a).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-04", Name = "[Abnormal] A nourishing element is never charged the clash penalty",
            Ideal = Fx.Of(FengShuiElement.Kim), Current = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Thuy), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = 0.400m,
            Why = "0.5×(0.000) + 0.5×(+0.800) = 0.400. Thủy sinh Mộc ⇒ không BiKhac ⇒ penalty 0. "
                + "Thủy chính là HÀNH HÓA GIẢI (Kim sinh Thủy, Thủy sinh Mộc) và engine tự xếp nó cao nhất — §13.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-05", Name = "[Boundary] Carry keeps AlwaysHard regardless of Wp",
            Placement = ProductPlacement.Carry, PersonalNeed = Fx.Of(FengShuiElement.Moc),
            Ideal = Fx.Of(FengShuiElement.Kim), Current = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = null,
            Why = "L2 chỉ thay None ở nhánh workspace; Carry giữ AlwaysHard (§14.6 #4).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-08", Name = "[Boundary] A public space neither filters nor penalises a destiny clash",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Public, Wp = 0.00m, Expected = -0.500m,
            Why = "Q12/§14.3: Public ⇒ PersonalConflictMode.None ⇒ chỉ còn gapScore −0.500. "
                + "Nếu vẫn là ByScope (bất nhất có sẵn từ v3.1) sẽ trừ ĐỦ 0.60 ⇒ −1.100 ⇒ clamp −1.000 "
                + "— tức là neo không gian chung vào bản mệnh của MỘT người.",
        });

        return data;
    }

    // ===================== A3-06. Bất biến của thang điểm mới =====================

    /// <summary>
    /// Hợp đồng của chuẩn hoá v3.2: với hai vector trạng thái Σ=1 khác nhau hoàn toàn, sản phẩm bù
    /// ĐÚNG hành phòng thiếu phải đạt <b>đúng +1.000</b>, và sản phẩm bơm thêm hành đã thừa phải đạt
    /// <b>đúng −1.000</b>. Trước v3.2 hai đầu này bị kẹt ở ±0.5 nên tier "Rất hợp" (≥ 0.6) là bất khả thi.
    /// <para>Dùng <see cref="ProductPlacement.Living"/> để tắt Directional Validation ⇒ điểm còn lại
    /// đúng bằng gapScore.</para>
    /// </summary>
    [Fact(DisplayName = "SCORE-A3-06 [Boundary] The normalised gap score reaches both ends of the range")]
    public void GapScore_ForOppositeSingleElementRooms_ReachesPlusAndMinusOne()
    {
        var elements = Enum.GetValues<FengShuiElement>();
        var scorer = new RecommendationScorer();

        foreach (var ideal in elements)
        foreach (var current in elements)
        {
            var ctx = new ScoringContext
            {
                AdjustedIdeal = ElementVector.Single(ideal),
                CurrentVector = ElementVector.Single(current),
                Scope = WorkspaceScope.Public,
                Purpose = WorkPurpose.Other,
                Params = ScoringParameters.Default,
            };

            decimal Score(FengShuiElement product) => scorer
                .Score(ctx, new[]
                {
                    new ProductFacts(ProductId, ElementVector.Single(product),
                        new HashSet<string>(), ProductPlacement.Living),
                })
                .Single().Score;

            if (ideal == current)
            {
                Assert.Equal(0.000m, Score(ideal));
                continue;
            }

            Assert.Equal(1.000m, Score(ideal));    // bù đúng hành đang thiếu
            Assert.Equal(-1.000m, Score(current)); // bơm thêm hành đã thừa
        }
    }

    // ===================== Fit: trang chi tiết sản phẩm không bao giờ loại =====================

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
            Placement = ProductPlacement.Consumable, Expected = 0.600m,
            Why = "Hợp đồng của ScoreSingle: trang chi tiết sản phẩm LUÔN có kết quả, placement chỉ sinh caution.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A4-07", Name = "[Abnormal] Fit mode scores a carry item against the room and warns about it",
            Placement = ProductPlacement.Carry, Expected = 0.600m,
            Why = "Fit luôn dùng PlacementPolicy.WorkspaceFit ⇒ chấm theo gap phòng + caution.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A5-06", Name = "[Abnormal] Fit mode keeps a vibe mismatch even with the hard filter on",
            Purpose = WorkPurpose.Office, Vibes = new[] { "Relax" }, VibeFilterHard = 1.00m,
            Expected = 0.600m,
            Why = "Loại cứng chỉ áp cho mode Rank; Fit vẫn trả điểm, chỉ thêm caution.",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-A2-05", Name = "[Abnormal] Fit mode charges the full penalty while the personal axis is off",
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.00m, Expected = -1.000m,
            Why = "−0.500 − 0.600 = −1.100 ⇒ clamp −1.000. Wp = 0 ⇒ ByScope ⇒ penalty ĐẦY ĐỦ (hành vi v3).",
        });
        data.Add(new ScoreCase
        {
            Id = "SCORE-L2-06", Name = "[Abnormal] Fit mode scales the clash penalty with Wp too",
            Ideal = Fx.Of(FengShuiElement.Kim), Current = Fx.Of(FengShuiElement.Moc),
            Product = Fx.Of(FengShuiElement.Kim), Personal = FengShuiElement.Moc,
            Scope = WorkspaceScope.Private, Wp = 0.50m, Expected = -0.300m,
            Why = "WorkspaceFit(personalBlendActive: true) ⇒ Scaled, không loại nhưng vẫn trừ 0.60×0.50 = 0.300 (§14.6 #5).",
        });

        return data;
    }

    // ===================== Hợp đồng cấp danh sách =====================

    /// <summary>
    /// Lý do tồn tại của v3.1: hai người khác bản mệnh, cùng một phòng, cùng một catalog thì thứ hạng
    /// PHẢI khác nhau. Nếu ca này xanh khi <c>PERSONAL_WEIGHT_*</c> = 0 thì trục cá nhân đang vô tác dụng.
    /// </summary>
    [Fact(DisplayName = "SCORE-B-01 [Normal] Two users with different destinies get different rankings in the same room")]
    public void Score_TwoDestiniesSameRoom_ProducesDifferentRanking()
    {
        var scorer = new RecommendationScorer();
        var wood = new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Moc), new HashSet<string>());
        var metal = new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Kim), new HashSet<string>());
        var water = new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Thuy), new HashSet<string>());
        var earth = new ProductFacts(Guid.NewGuid(), Fx.Of(FengShuiElement.Tho), new HashSet<string>());
        var candidates = new[] { wood, metal, water, earth };

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

        var forWood = scorer.Score(For(FengShuiElement.Moc), candidates).Select(s => s.ProductId).ToList();
        var forMetal = scorer.Score(For(FengShuiElement.Kim), candidates).Select(s => s.ProductId).ToList();

        // Mệnh Mộc: Mộc 0.800 > Thủy 0.600 > Thổ −0.150 > Kim −1.000
        Assert.Equal(wood.ProductId, forWood[0]);
        Assert.Equal(metal.ProductId, forWood[^1]);

        // Mệnh Kim: Mộc 0.400 > Kim 0.250 > Thổ 0.150 > Thủy 0.100
        Assert.Equal(metal.ProductId, forMetal[1]);

        Assert.NotEqual(forWood, forMetal);
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
/// Bộ số cố định của mọi ca. Chọn để KỲ VỌNG RA SỐ TRÒN, tính tay được trên giấy:
/// <code>
/// adjustedIdeal = { Mộc 0.6, Thủy 0.4 }        current = { Kim 0.5, Thổ 0.5 }
/// gap           = Mộc +0.6, Thủy +0.4, Kim −0.5, Thổ −0.5     |gap|₁ = 2.0  ⇒  mẫu số = 1.0
///
/// SP thuần      gapScore (v3.2)   personalScore (mệnh Mộc)
/// Mộc            +0.600            +1.0   tỷ hòa
/// Thủy           +0.400            +0.8   Thủy sinh Mộc
/// Hỏa             0.000            −0.2   Mộc sinh Hỏa (tiết khí)
/// Thổ            −0.500            +0.2   Mộc khắc Thổ
/// Kim            −0.500            −1.0   Kim khắc Mộc      ← BiKhac, dính L2
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
/// đích (không lọc vibe) + không hướng bị chắn — mỗi ca chỉ khai ĐÚNG những gì nó thay đổi.
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
