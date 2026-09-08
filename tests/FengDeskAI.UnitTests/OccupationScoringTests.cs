using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// P5 — nghề nghiệp bẻ vector điểm quan hệ <c>r</c> (ADR v3.2 §11, phương án N1).
///
/// <para>Công thức đang khẳng định:</para>
/// <code>
/// r'[e] = clamp(r[e] + delta[e]·OCCUPATION_SHARE, −1, 1)
/// r'[e] = min(r'[e], −0.1)   khi GetRelation(mệnh, e) == BiKhac   // nghề không đổi được bản mệnh
/// d     = (1 − Wp)·ĝ + Wp·r'
/// </code>
///
/// <para>
/// Mọi ca dựng phòng có <c>adjustedIdeal ≡ current</c> ⇒ <c>gap = 0</c> ⇒ <c>ĝ = 0</c>, nên
/// <c>d·p = Wp·r'[e]</c> và kỳ vọng tính tay được từ đúng một số hạng. Placement
/// <see cref="ProductPlacement.Living"/> tắt Directional Validation, <see cref="WorkPurpose.Other"/>
/// tắt luật vibe — điểm còn lại chỉ là phần đang kiểm.
/// </para>
/// </summary>
public sealed class OccupationScoringTests
{
    private static readonly Guid ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const decimal Wp = 0.50m;

    /// <summary>Phòng "phẳng": gap = 0 nên ĝ = 0, mọi thứ còn lại đến từ trục cá nhân.</summary>
    private static readonly ElementVector FlatRoom = new(Tho: 0.2m, Kim: 0.2m, Thuy: 0.2m, Moc: 0.2m, Hoa: 0.2m);

    private static ScoringContext ContextOf(
        FengShuiElement destiny,
        ElementVector? occupationDelta = null,
        decimal occupationShare = 0m,
        ElementVector? personalNeed = null) => new()
    {
        AdjustedIdeal = FlatRoom,
        CurrentVector = FlatRoom,
        PersonalVector = ElementVector.Single(destiny),
        PersonalNeedVector = personalNeed,
        Scope = WorkspaceScope.Private,
        Purpose = WorkPurpose.Other,
        PersonalWeight = Wp,
        OccupationDelta = occupationDelta,
        OccupationCode = occupationDelta is null ? null : "IT",
        OccupationNameVi = occupationDelta is null ? null : "CNTT / Lập trình",
        Params = ScoringParameters.Default with { OccupationShare = occupationShare },
    };

    private static ScoredProduct Score(
        FengShuiElement destiny,
        FengShuiElement product,
        ElementVector? occupationDelta = null,
        decimal occupationShare = 0m,
        ProductPlacement placement = ProductPlacement.Living)
    {
        var facts = new ProductFacts(ProductId, ElementVector.Single(product), new HashSet<string>(), placement);
        var scored = new RecommendationScorer()
            .Score(ContextOf(destiny, occupationDelta, occupationShare), new[] { facts })
            .FirstOrDefault();
        Assert.NotNull(scored);
        return scored!;
    }

    /// <summary>Hành đầu tiên KHẮC bản mệnh — suy từ luật, không hard-code, để ca không mục khi bảng đổi.</summary>
    private static FengShuiElement ClashingElement(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .First(e => FengShuiCalculator.GetRelation(destiny, e) == FengShuiRelation.BiKhac);

    /// <summary>Hành bản mệnh ƯA (không khắc, không phải chính nó) — nơi delta được phép tác động đủ.</summary>
    private static FengShuiElement FriendlyElement(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .First(e => e != destiny && FengShuiCalculator.GetRelation(destiny, e) != FengShuiRelation.BiKhac);

    // ===================== Kill-switch =====================

    /// <summary>
    /// <c>OCCUPATION_SHARE = 0</c> phải cho kết quả <b>y hệt</b> khi chưa có P5, kể cả khi bảng delta đã
    /// đầy dữ liệu. Đây là điều kiện để merge P5 mà không phải chạy lại golden set: tắt công tắc thì
    /// mọi con số cũ phải giữ nguyên tới từng chữ số.
    /// </summary>
    [Fact(DisplayName = "SCORE-OCC-01 [Boundary] A zero occupation share leaves every number untouched")]
    public void OccupationShare_WhenZero_ProducesIdenticalScoreAndBreakdown()
    {
        var delta = new ElementVector(Tho: 0.5m, Kim: -0.5m, Thuy: 0.5m, Moc: 0.5m, Hoa: -0.5m);

        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        foreach (var product in Enum.GetValues<FengShuiElement>())
        {
            var without = Score(destiny, product);
            var with = Score(destiny, product, delta, occupationShare: 0m);

            Assert.Equal(without.Score, with.Score);
            Assert.Equal(without.Breakdown!.RuleScoreVector, with.Breakdown!.RuleScoreVector);
            Assert.Null(with.Breakdown.OccupationShift);
            Assert.Null(with.Breakdown.OccupationCode);
        }
    }

    /// <summary>
    /// Nghề đã khai nhưng <b>chưa có delta nào</b> cũng phải là không-tác-động. Chuyên gia chưa duyệt
    /// bảng số thì nghề chỉ là một nhãn hồ sơ, không được lén đổi thứ hạng.
    /// </summary>
    [Fact(DisplayName = "SCORE-OCC-02 [Boundary] An occupation with a zero delta vector changes nothing")]
    public void OccupationDelta_WhenAllZero_DoesNotMoveTheScore()
    {
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            var friendly = FriendlyElement(destiny);
            Assert.Equal(
                Score(destiny, friendly).Score,
                Score(destiny, friendly, ElementVector.Zero, occupationShare: 1m).Score);
        }
    }

    // ===================== Tác động tuyến tính =====================

    /// <summary>
    /// Lý do chọn N1 thay vì bẻ <c>personalVector</c>: tác động phải <b>tuyến tính và liên tục</b>.
    /// Với <c>ĝ = 0</c> và sản phẩm thuần một hành, điểm dịch đúng <c>Wp × delta × share</c> — không
    /// nhảy bậc, không phụ thuộc việc delta có đủ lớn để lật đỉnh hay không.
    /// </summary>
    [Theory(DisplayName = "SCORE-OCC-03 [Normal] The occupation delta shifts the score linearly")]
    [InlineData(0.20, 1.00, 0.100)]
    [InlineData(0.20, 0.50, 0.050)]
    [InlineData(-0.20, 1.00, -0.100)]
    public void OccupationDelta_ShiftsScoreBy_PersonalWeightTimesDeltaTimesShare(
        double delta, double share, double expectedShift)
    {
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            var friendly = FriendlyElement(destiny);
            var deltaVector = ElementVector.Single(friendly).Scale((decimal)delta);

            decimal baseline = Score(destiny, friendly).Score;
            decimal shifted = Score(destiny, friendly, deltaVector, (decimal)share).Score;

            Assert.Equal((decimal)expectedShift, Math.Round(shifted - baseline, 3));
        }
    }

    // ===================== Ràng buộc kiêng kỵ =====================

    /// <summary>
    /// <b>SCORE-L2-07</b> (§14.7, hoãn từ P1 sang P5). Nghề nghiệp KHÔNG đổi được bản mệnh: dù khai
    /// <c>delta = +1.0</c> và mở hết <c>OCCUPATION_SHARE = 1.0</c>, hành đang <c>BiKhac</c> vẫn phải
    /// nằm ở <c>≤ −0.1</c>. Không có chặn này thì một dòng delta gõ sai đủ để hệ thống đi gợi ý đúng
    /// thứ người dùng phải kiêng.
    /// </summary>
    [Fact(DisplayName = "SCORE-L2-07 [Boundary] An occupation cannot lift a clashing element out of the negative")]
    public void OccupationDelta_CannotRaiseAClashingElementAboveTheCap()
    {
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            var clashing = ClashingElement(destiny);
            var scored = Score(destiny, clashing, ElementVector.Single(clashing), occupationShare: 1m);

            var adjusted = scored.Breakdown!.RuleScoreVector!.Value[clashing];
            Assert.True(adjusted <= -0.1m,
                $"Mệnh {destiny}, hành khắc {clashing}: r' = {adjusted}, phải ≤ −0.1 dù delta = +1.0.");
        }
    }

    /// <summary>
    /// Phạt điểm khi sản phẩm khắc mệnh là một <b>phạm trù</b> (<c>GetRelation</c>), không phải một
    /// con số liên tục — nên delta nghề nghiệp không được chạm vào nó. Ca này khoá đúng ranh giới
    /// phân vai ở §14.4: nghề đổi mức ƯA THÍCH, không đổi mức KIÊNG KỴ.
    /// </summary>
    [Fact(DisplayName = "SCORE-OCC-04 [Abnormal] An occupation never softens the destiny-clash penalty")]
    public void OccupationDelta_DoesNotChangeTheUserConflictPenalty()
    {
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            var clashing = ClashingElement(destiny);

            decimal Penalty(ScoredProduct s) => s.Breakdown!.UserPenalty;

            Assert.Equal(
                Penalty(Score(destiny, clashing)),
                Penalty(Score(destiny, clashing, ElementVector.Single(clashing), occupationShare: 1m)));
        }
    }

    /// <summary>
    /// <c>OccupationShift = r' − r</c> phải là mức dịch <b>THẬT</b>, tức sau khi chặn. Trục bị chặn
    /// hiện gần 0 chứ không hiện <c>delta·share</c> danh nghĩa — FE vẽ lớp nghề nghiệp từ đây, và một
    /// lớp nói "nghề của bạn nâng Kim" trong khi Kim vẫn khắc mệnh là nói dối bằng đồ hoạ.
    /// </summary>
    [Fact(DisplayName = "SCORE-OCC-05 [Normal] The reported shift is the real one, measured after clamping")]
    public void OccupationShift_ReportsTheEffectiveShift_NotTheNominalDelta()
    {
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            var clashing = ClashingElement(destiny);
            var friendly = FriendlyElement(destiny);
            var delta = ElementVector.Single(clashing).Add(ElementVector.Single(friendly).Scale(0.2m));

            var scored = Score(destiny, friendly, delta, occupationShare: 1m);
            var breakdown = scored.Breakdown!;
            var shift = breakdown.OccupationShift!.Value;

            Assert.Equal("IT", breakdown.OccupationCode);
            Assert.Equal(1m, breakdown.OccupationShare);

            // Hành được ưa: dịch đúng delta đã khai.
            Assert.Equal(0.2m, Math.Round(shift[friendly], 3));

            // Hành khắc mệnh: r bị chặn nên phần dịch thật nhỏ hơn hẳn delta danh nghĩa 1.0.
            Assert.True(shift[clashing] < 1m,
                $"Mệnh {destiny}: trục {clashing} báo dịch {shift[clashing]}, đáng ra phải bị chặn.");
            Assert.Equal(
                breakdown.RuleScoreVector!.Value[clashing] - breakdown.BaseRuleScoreVector!.Value[clashing],
                shift[clashing]);
        }
    }

    // ===================== Ranh giới luồng =====================

    /// <summary>
    /// Luồng <see cref="ProductPlacement.Carry"/> chấm theo vector dụng thần, KHÔNG dựng <c>r</c> —
    /// nên N1 không có chỗ bám và nghề nghiệp không tác động. Ca này khoá điều đó lại thành hành vi
    /// có chủ ý: muốn nghề nghiệp vào luồng Carry thì phải bẻ chính vector dụng thần, một quyết định
    /// nghiệp vụ khác chưa chốt (§11.2).
    /// </summary>
    [Fact(DisplayName = "SCORE-OCC-06 [Boundary] The carry flow is untouched by occupation")]
    public void OccupationDelta_DoesNotAffectTheCarryFlow()
    {
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            var friendly = FriendlyElement(destiny);
            var need = ElementVector.Single(friendly);
            var facts = new ProductFacts(
                ProductId, ElementVector.Single(friendly), new HashSet<string>(), ProductPlacement.Carry);

            decimal ScoreCarry(ElementVector? delta, decimal share)
            {
                var ctx = ContextOf(destiny, delta, share, personalNeed: need);
                var scored = new RecommendationScorer().Score(ctx, new[] { facts }).FirstOrDefault();
                Assert.NotNull(scored);
                return scored!.Score;
            }

            Assert.Equal(
                ScoreCarry(null, 0m),
                ScoreCarry(ElementVector.Single(friendly).Scale(0.5m), 1m));
        }
    }
}
