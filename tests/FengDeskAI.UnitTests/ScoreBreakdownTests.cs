using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// P3 — <b>bất biến của <see cref="ScoreBreakdown"/></b> (v3.2 §9.1). Breakdown là thứ FE dựng waterfall
/// và radar từ đó; nếu các số hạng không cộng lại đúng bằng điểm hiển thị thì màn hình đang giải thích
/// một phép tính KHÁC với phép tính đã xếp hạng sản phẩm — sai lầm nguy hiểm hơn là không giải thích gì.
///
/// <para>Bốn đẳng thức được khoá, chạy trên MỌI tổ hợp chứ không phải vài ca lẻ:</para>
/// <code>
/// Σ Components[i].Contribution                            == Blended
/// ProductVector · CombinedDirection                       ≈  Blended
/// Blended − UserPenalty − DirectionPenalty − VibePenalty  == RawScore
/// round(clamp(RawScore, −1, 1), 3)                        == Score
/// </code>
/// </summary>
public sealed class ScoreBreakdownTests
{
    /// <summary>
    /// Sai số cho phép ở đẳng thức vector. <c>Blended</c> đi đường vô hướng
    /// (<c>(1−Wp)·gapScore + Wp·personalScore</c>), còn <c>p·d</c> đi đường vector — hai đường nhân/chia
    /// decimal theo thứ tự khác nhau nên lệch ở những chữ số cuối. Ngưỡng này nhỏ hơn <b>một triệu lần</b>
    /// chữ số thứ 3 mà điểm được làm tròn tới, nên vẫn bắt được mọi sai sót thật (nhầm <c>Wp</c>, nhầm
    /// <c>r</c>, nhầm <c>ĝ</c> đều lệch ở hàng phần trăm trở lên).
    /// </summary>
    private const decimal VectorTolerance = 0.000_000_001m;

    private static readonly ElementVector Ideal = new(Tho: 0m, Kim: 0m, Thuy: 0.4m, Moc: 0.6m, Hoa: 0m);
    private static readonly ElementVector Current = new(Tho: 0.5m, Kim: 0.5m, Thuy: 0m, Moc: 0m, Hoa: 0m);

    /// <summary>
    /// Quét toàn bộ không gian tổ hợp có ý nghĩa: 3 scope × 4 mức <c>Wp</c> × 6 bản mệnh (kể cả "chưa có
    /// ngày sinh") × 5 hành sản phẩm × 2 placement × 2 mục đích phòng. Ca nào bị loại thì bỏ qua — phần
    /// còn lại phải thoả cả bốn đẳng thức.
    /// </summary>
    [Fact(DisplayName = "SCORE-BD-01 [Normal] Every breakdown adds up to the score it explains")]
    public void Breakdown_AcrossTheWholeParameterSpace_ReconstructsTheScore()
    {
        var scorer = new RecommendationScorer();
        int checkedCases = 0;

        foreach (var scope in Enum.GetValues<WorkspaceScope>())
        foreach (var wp in new[] { 0m, 0.30m, 0.50m, 1.00m })
        foreach (var destiny in Destinies())
        foreach (var productElement in Enum.GetValues<FengShuiElement>())
        foreach (var placement in new[] { ProductPlacement.Desk, ProductPlacement.Living })
        foreach (var purpose in new[] { WorkPurpose.Other, WorkPurpose.Office })
        {
            var ctx = new ScoringContext
            {
                AdjustedIdeal = Ideal,
                CurrentVector = Current,
                PersonalVector = destiny is { } d ? ElementVector.Single(d) : null,
                Scope = scope,
                Purpose = purpose,
                PersonalWeight = wp,
                // Hạ filter cứng để sản phẩm lệch vibe CÒN LẠI mà kiểm tra — mục tiêu ở đây là bất biến
                // số học, không phải luật lọc (luật lọc đã có bộ ca A5 riêng).
                Params = ScoringParameters.Default with { VibeFilterHard = 0m },
                ViolatedDirections = new HashSet<CompassDirection>
                {
                    CompassDirection.East, CompassDirection.Southeast, CompassDirection.North,
                },
            };

            var product = new ProductFacts(
                Guid.NewGuid(), ElementVector.Single(productElement), new HashSet<string>(), placement);

            var scored = scorer.Score(ctx, new[] { product }).FirstOrDefault();
            if (scored is null) continue; // bị loại (khắc mệnh ở phòng riêng khi Wp = 0) — không có gì để đối chiếu

            AssertConsistent(scored, $"scope={scope} wp={wp} mệnh={destiny?.ToString() ?? "—"} "
                + $"sp={productElement} placement={placement} purpose={purpose}");
            checkedCases++;
        }

        Assert.True(checkedCases > 500, $"Chỉ kiểm được {checkedCases} tổ hợp — ma trận quét bị thu hẹp ngoài ý muốn.");
    }

    /// <summary>
    /// Luồng <see cref="ProductPlacement.Carry"/> đi nhánh <see cref="ScoringTarget.PersonalNeed"/>:
    /// mẫu số KHÔNG chia đôi, <c>Wp</c> không áp, waterfall chỉ có MỘT thành phần. Bất biến vẫn phải đúng
    /// — nếu không, UI riêng của Carry (§PHẦN E #6) sẽ hiện một phép cộng không khớp điểm.
    /// </summary>
    [Fact(DisplayName = "SCORE-BD-02 [Normal] A carry item's single-component breakdown still adds up")]
    public void Breakdown_ForCarryItems_HasOneComponentAndStillAddsUp()
    {
        var scorer = new RecommendationScorer();

        foreach (var need in Enum.GetValues<FengShuiElement>())
        foreach (var productElement in Enum.GetValues<FengShuiElement>())
        {
            var ctx = new ScoringContext
            {
                AdjustedIdeal = Ideal,
                CurrentVector = Current,
                PersonalNeedVector = ElementVector.Single(need),
                PersonalVector = ElementVector.Single(need),
                Scope = WorkspaceScope.Private,
                Purpose = WorkPurpose.Other,
                PersonalWeight = 0.50m,
                Params = ScoringParameters.Default,
            };

            var product = new ProductFacts(
                Guid.NewGuid(), ElementVector.Single(productElement), new HashSet<string>(), ProductPlacement.Carry);

            var scored = scorer.Score(ctx, new[] { product }).FirstOrDefault();
            if (scored is null) continue; // AlwaysHard loại thẳng sản phẩm khắc mệnh

            var breakdown = AssertConsistent(scored, $"dụng thần={need} sp={productElement}");

            Assert.Equal(ScoringTarget.PersonalNeed, breakdown.Target);
            Assert.Equal(ScoreComponentCodes.PersonalNeedScore, Assert.Single(breakdown.Components).Code);
            Assert.Equal(0m, breakdown.PersonalWeight);
            Assert.Null(breakdown.PersonalWeightCode);
            Assert.NotNull(breakdown.PersonalNeedVector);
        }
    }

    /// <summary>
    /// <c>priorityVector</c> = <c>normalize(max(d, 0))</c> — lớp vàng trên radar (§10.3). Phải Σ=1 và
    /// không âm để chồng được lên <c>adjustedIdeal</c>/<c>current</c> cùng thang; và phải BỎ đúng những
    /// hành mà <c>d</c> đang âm, vì đó là hành hệ thống đang tránh chứ không phải đang ưu tiên.
    /// </summary>
    [Fact(DisplayName = "SCORE-BD-03 [Boundary] The priority vector keeps only what the engine favours")]
    public void PriorityVector_DropsNegativeAxes_AndSumsToOne()
    {
        var scorer = new RecommendationScorer();
        var ctx = new ScoringContext
        {
            AdjustedIdeal = Ideal,
            CurrentVector = Current,
            PersonalVector = ElementVector.Single(FengShuiElement.Moc),
            Scope = WorkspaceScope.Private,
            Purpose = WorkPurpose.Other,
            PersonalWeight = 0.50m,
            Params = ScoringParameters.Default,
        };

        var scored = scorer.ScoreSingle(
            ctx, new ProductFacts(Guid.NewGuid(), ElementVector.Single(FengShuiElement.Moc), new HashSet<string>()));

        var breakdown = scored.Breakdown!;
        var priority = breakdown.PriorityVector;

        Assert.Equal(1.000m, Math.Round(priority.L1(), 3));
        foreach (var (element, value) in priority.Enumerate())
        {
            Assert.True(value >= 0m, $"{element}: priorityVector không được âm.");
            if (breakdown.CombinedDirection[element] < 0m)
                Assert.Equal(0m, value); // hành đang bị trừ điểm không được vẽ như hành ưu tiên
        }
    }

    /// <summary>
    /// §13 — phòng thiếu đúng hành khắc mệnh thì engine phải NÓI RA hành hoá giải, và hành đó phải thật
    /// sự là cầu nối: <c>roomNeed sinh bridge</c> và <c>bridge sinh destiny</c>. Ca ngược lại (phòng
    /// thiếu hành thuận mệnh) phải trả <c>null</c>, không được cảnh báo thừa.
    /// </summary>
    [Fact(DisplayName = "SCORE-BD-04 [Abnormal] A room needing the element that clashes gets a bridge element")]
    public void ConflictResolution_WhenRoomNeedsTheClashingElement_NamesTheBridge()
    {
        var scorer = new RecommendationScorer();

        ScoreBreakdown Run(ElementVector ideal, ElementVector current, FengShuiElement destiny) => scorer.ScoreSingle(
            new ScoringContext
            {
                AdjustedIdeal = ideal,
                CurrentVector = current,
                PersonalVector = ElementVector.Single(destiny),
                Scope = WorkspaceScope.Private,
                Purpose = WorkPurpose.Other,
                PersonalWeight = 0.50m,
                Params = ScoringParameters.Default,
            },
            new ProductFacts(Guid.NewGuid(), ElementVector.Single(FengShuiElement.Thuy), new HashSet<string>()))
            .Breakdown!;

        // Phòng cần Kim, mệnh Mộc — Kim khắc Mộc.
        var conflict = Run(ElementVector.Single(FengShuiElement.Kim), ElementVector.Single(FengShuiElement.Moc),
            FengShuiElement.Moc).ConflictResolution;

        Assert.NotNull(conflict);
        Assert.Equal(FengShuiElement.Kim, conflict!.RoomNeed);
        Assert.Equal(FengShuiElement.Moc, conflict.Destiny);
        Assert.Equal(FengShuiElement.Thuy, conflict.Bridge);

        // Quy luật ngũ hành: A khắc B ⇒ con của A = mẹ của B. Cầu nối luôn tồn tại và luôn duy nhất.
        Assert.Equal(conflict.Bridge, FengShuiCalculator.GetGeneratedElement(conflict.RoomNeed));
        Assert.Equal(conflict.Bridge, FengShuiCalculator.GetGeneratingElement(conflict.Destiny));

        // Phòng cần Thủy, mệnh Mộc — Thủy sinh Mộc, không có gì để hoá giải.
        Assert.Null(Run(ElementVector.Single(FengShuiElement.Thuy), ElementVector.Single(FengShuiElement.Hoa),
            FengShuiElement.Moc).ConflictResolution);
    }

    /// <summary>
    /// Penalty phải được liệt kê ĐỦ LOẠI kể cả khi không bị áp — "đã xét và không trừ" khác hẳn "không
    /// tồn tại". Accordion của FE (§P4.6) dựa vào đây để user không nghi có luật ẩn.
    /// </summary>
    [Fact(DisplayName = "SCORE-BD-05 [Normal] Penalties are always listed, applied or not")]
    public void Penalties_AreAlwaysListedWithReasons_EvenWhenNotApplied()
    {
        var scored = new RecommendationScorer().ScoreSingle(
            new ScoringContext
            {
                AdjustedIdeal = Ideal,
                CurrentVector = Current,
                PersonalVector = ElementVector.Single(FengShuiElement.Moc),
                Scope = WorkspaceScope.Private,
                Purpose = WorkPurpose.Other,
                PersonalWeight = 0.50m,
                Params = ScoringParameters.Default,
            },
            new ProductFacts(Guid.NewGuid(), ElementVector.Single(FengShuiElement.Moc), new HashSet<string>()));

        var penalties = scored.Breakdown!.Penalties;

        Assert.Equal(3, penalties.Count);
        Assert.All(penalties, p => Assert.False(string.IsNullOrWhiteSpace(p.ReasonVi)));
        Assert.All(penalties, p => Assert.Equal(p.Value > 0m, p.Applied));
        Assert.Contains(penalties, p => p.Code == ScoringParamCodes.UserConflictPenalty);
        Assert.Contains(penalties, p => p.Code == ScoringParamCodes.DirectionPenalty);
        Assert.Contains(penalties, p => p.Code is ScoringParamCodes.VibeMismatchPenalty or ScoringParamCodes.VibeUnknownPenalty);

        Assert.All(scored.Breakdown!.Components, c => Assert.False(string.IsNullOrWhiteSpace(c.ReasonVi)));
        Assert.Equal(ScoringFormulaVersions.V32, scored.Breakdown!.FormulaVersion);
    }

    /// <summary>Bốn đẳng thức của §9.1, dùng chung cho mọi ca.</summary>
    private static ScoreBreakdown AssertConsistent(ScoredProduct scored, string because)
    {
        var b = scored.Breakdown;
        Assert.True(b is not null, $"{because}: thiếu breakdown.");

        decimal sumComponents = b!.Components.Sum(c => c.Contribution);
        Assert.True(Math.Abs(sumComponents - b.Blended) <= VectorTolerance,
            $"{because}: Σ contribution = {sumComponents} ≠ blended {b.Blended}.");

        decimal viaVector = b.ProductVector.Dot(b.CombinedDirection);
        Assert.True(Math.Abs(viaVector - b.Blended) <= VectorTolerance,
            $"{because}: p·d = {viaVector} ≠ blended {b.Blended} — radar sẽ vẽ một phép tính khác điểm.");

        Assert.Equal(b.Blended - b.UserPenalty - b.DirectionPenalty - b.VibePenalty, b.RawScore);
        Assert.Equal(Math.Round(Math.Clamp(b.RawScore, -1m, 1m), 3), scored.Score);
        Assert.Equal(b.Clamped, b.RawScore is < -1m or > 1m);

        return b;
    }

    /// <summary>5 bản mệnh + <c>null</c> = user chưa khai ngày sinh (trục cá nhân tắt cứng).</summary>
    private static IEnumerable<FengShuiElement?> Destinies()
        => Enum.GetValues<FengShuiElement>().Select(e => (FengShuiElement?)e).Append(null);
}
