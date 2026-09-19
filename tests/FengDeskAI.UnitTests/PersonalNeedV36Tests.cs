using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// v3.6 — nhánh Carry (ADR <c>personal-need-v3.6.md</c>): dụng thần có kỵ thần, điểm đo "phủ" thay tích trong.
///
/// <code>
/// needCover = Σ_e min(n̂[e], p[e])         avoidHit = Σ_{e ∈ kỵ} p[e]
/// personal  = needCover − avoidHit          blended = (1 − Wo)·personal + Wo·(ô·p)
/// MINOR_CLASH chỉ áp cho hành khắc mệnh KHÔNG nằm trong kỵ (không trừ hai lần)
/// </code>
///
/// <para>
/// Kỳ vọng tính TAY từ bảng §2.3 của ADR, trên chính vector 8 sản phẩm Carry trong DB dev và hồ sơ thật
/// (sinh 2001-05-08 14:30 — nhật chủ Tân/Kim, thân nhược ⇒ dụng Thổ .6 / Kim .4, kỵ Thủy · Mộc · Hỏa).
/// </para>
/// </summary>
public sealed class PersonalNeedV36Tests
{
    private static readonly DateTime Dob = new(2001, 5, 8, 7, 0, 0);
    private static readonly TimeOnly BirthTime = new(14, 30);

    private static readonly ScoringParameters Prms = ScoringParameters.Default with { OccupationWeight = 0m };

    private static ElementVector V(decimal kim = 0, decimal moc = 0, decimal thuy = 0, decimal hoa = 0, decimal tho = 0)
        => new(Tho: tho, Kim: kim, Thuy: thuy, Moc: moc, Hoa: hoa);

    /// <summary>8 vật Carry trong DB dev (sau khi đồng bộ seed 2026-09-19) + hai ca biên.</summary>
    public static TheoryData<string, ElementVector, decimal, decimal, decimal> TuTruCases() => new()
    {
        // tên, p, needCover, avoidHit, score kỳ vọng (không có clash ngoài kỵ ⇒ score = cover − hit)
        { "Tỳ hưu bạc",         V(kim: .7m, tho: .3m),                       0.700m, 0.000m, 0.700m },
        { "Vòng thạch anh tím", V(kim: .2m, hoa: .2m, tho: .6m),             0.800m, 0.200m, 0.600m },
        { "Tượng Tỳ Hưu đồng",  V(kim: .58m, hoa: .12m, tho: .3m),           0.700m, 0.120m, 0.580m },
        { "Charm hồ lô đồng",   V(kim: .567m, moc: .18m, hoa: .253m),        0.400m, 0.433m, -0.033m },
        { "Charm obsidian",     V(kim: .24m, thuy: .56m, tho: .2m),          0.440m, 0.560m, -0.120m },
        { "Móc khóa gỗ",        V(moc: .6m, tho: .4m),                       0.400m, 0.600m, -0.200m },
        { "Vòng gỗ sưa đỏ",     V(kim: .2m, moc: .6m, hoa: .14m, tho: .06m), 0.260m, 0.740m, -0.480m },
        { "khớp đúng Thổ.6 Kim.4", V(kim: .4m, tho: .6m),                    1.000m, 0.000m, 1.000m },
        { "100% Thủy",          V(thuy: 1m),                                 0.000m, 1.000m, -1.000m },
    };

    private static PersonalTarget TuTruTarget() => PersonalTargetBuilder.Build(Dob, BirthTime, Prms)!;

    private static ScoringContext ContextOf(PersonalTarget target) => new()
    {
        PersonalVector = FengShuiCalculator.BuildPersonalVector(Dob, Prms.SelfShare, Prms.SupportShare, Prms.ChildShare),
        PersonalNeedVector = target.Vector,
        PersonalAvoid = target.AvoidSet,
        AdjustedIdeal = ElementVector.Zero,
        CurrentVector = ElementVector.Zero,
        Scope = WorkspaceScope.Private,
        Purpose = WorkPurpose.Other,
        PersonalWeight = 0m,
        Params = Prms,
    };

    private static ScoredProduct Fit(PersonalTarget target, ElementVector p)
        => new RecommendationScorer().ScoreSingle(ContextOf(target), new ProductFacts(Guid.NewGuid(), p, new HashSet<string>(), ProductPlacement.Carry));

    [Fact(DisplayName = "NEED-36-01 [Normal] The real chart yields need Thổ .6/Kim .4 and avoid Thủy·Mộc·Hỏa")]
    public void TuTru_RealChart_HasNeedAndAvoid()
    {
        var t = TuTruTarget();

        Assert.Equal(PersonalTargetSource.TuTru, t.Source);
        Assert.Equal(0.6m, t.Vector.Tho);
        Assert.Equal(0.4m, t.Vector.Kim);
        Assert.Equal(new HashSet<FengShuiElement> { FengShuiElement.Thuy, FengShuiElement.Moc, FengShuiElement.Hoa }, t.AvoidSet);
        Assert.Equal(new[] { "Mộc", "Thủy", "Hỏa" }, t.AvoidElements);
    }

    [Theory(DisplayName = "NEED-36-02 [Normal] Carry score = Σ min(n̂,p) − Σ_{kỵ} p on the real catalogue")]
    [MemberData(nameof(TuTruCases))]
    public void Carry_ScoresByCoverMinusAvoid(string name, ElementVector p, decimal cover, decimal hit, decimal expected)
    {
        var scored = Fit(TuTruTarget(), p);
        var b = scored.Breakdown!;

        Assert.Equal(cover, Math.Round(b.PersonalNeedCover!.Value, 3));
        Assert.Equal(hit, Math.Round(b.PersonalAvoidHit!.Value, 3));
        Assert.True(Math.Abs(scored.Score - expected) <= 0.001m, $"{name}: kỳ vọng {expected}, engine {scored.Score}");

        // Hai dòng waterfall cộng lại đúng blended; kỵ thần là số hạng CỘNG âm, không phải penalty.
        var need = b.Components.Single(c => c.Code == ScoreComponentCodes.PersonalNeedScore);
        var avoid = b.Components.Single(c => c.Code == ScoreComponentCodes.PersonalAvoidScore);
        Assert.Equal(cover, Math.Round(need.Value, 3));
        Assert.Equal(-hit, Math.Round(avoid.Value, 3));
        Assert.Equal(Math.Round(b.Blended, 6), Math.Round(b.Components.Sum(c => c.Contribution), 6));
    }

    [Fact(DisplayName = "NEED-36-03 [Boundary] The 0–100% range is reachable: perfect match = 1, all-avoid = −1")]
    public void Carry_Range_IsFullyReachable()
    {
        var t = TuTruTarget();
        Assert.Equal(1.000m, Fit(t, V(kim: .4m, tho: .6m)).Score);
        Assert.Equal(-1.000m, Fit(t, V(thuy: 1m)).Score);
    }

    [Fact(DisplayName = "NEED-36-04 [Normal] Over-supplying a needed element does not add beyond the need")]
    public void Carry_OverSupply_IsCapped()
    {
        // 100% Kim: nhu cầu Kim chỉ 0.4 ⇒ phủ 0.4, phần Kim thừa không cộng thêm (như "thêm thừa" bên phòng).
        var scored = Fit(TuTruTarget(), V(kim: 1m));
        Assert.Equal(0.400m, scored.Score);
    }

    [Fact(DisplayName = "NEED-36-05 [Normal] An element already in the avoid set is not clashed twice")]
    public void Carry_AvoidAndClash_NotDoubleCounted()
    {
        // Hỏa khắc Nạp Âm Kim VÀ là kỵ thần: bị trừ qua avoidHit, MINOR_CLASH phải bỏ qua nó.
        var scored = Fit(TuTruTarget(), V(kim: .567m, moc: .18m, hoa: .253m));
        var minor = scored.Breakdown!.Penalties.Single(p => p.Code == ScoringParamCodes.MinorClashPenalty);
        Assert.False(minor.Applied);
        Assert.Equal(-0.033m, scored.Score);
    }

    [Fact(DisplayName = "NEED-36-06 [Normal] Nạp Âm fallback avoids only the element that controls the destiny")]
    public void NapAm_Fallback_AvoidsControllerOnly()
    {
        var t = PersonalTargetBuilder.Build(Dob, null, Prms)!;

        Assert.Equal(PersonalTargetSource.NapAm, t.Source);
        Assert.Equal(new HashSet<FengShuiElement> { FengShuiElement.Hoa }, t.AvoidSet); // Hỏa khắc Kim

        // Charm hồ lô: phủ = min(.6,.567) + min(.1,0) + min(.3,0) = 0.567; kỵ = Hỏa .253 ⇒ 0.314.
        var scored = Fit(t, V(kim: .567m, moc: .18m, hoa: .253m));
        Assert.Equal(0.314m, scored.Score);
    }

    [Theory(DisplayName = "NEED-36-07 [Normal] Kỵ thần mirrors dụng thần for every day master and strength")]
    [InlineData(FengShuiElement.Kim, true)]
    [InlineData(FengShuiElement.Kim, false)]
    [InlineData(FengShuiElement.Moc, true)]
    [InlineData(FengShuiElement.Thuy, false)]
    public void BaTu_Unfavorable_IsSymmetricToFavorable(FengShuiElement dayMaster, bool strong)
    {
        // Suy lại bằng chính luật ngũ hành thay vì đóng đinh tên hành.
        var supporter = FengShuiCalculator.GetGeneratingElement(dayMaster);
        var expected = strong
            ? new[] { supporter, dayMaster }
            : new[]
            {
                FengShuiCalculator.GetGeneratedElement(dayMaster),
                FengShuiCalculator.GetControlledElement(dayMaster),
                FengShuiCalculator.GetControllingElement(dayMaster),
            };

        // Kỵ và dụng không được giao nhau, và hợp lại phải phủ ≥ 4 trong 5 hành (nhàn ≤ 1).
        var favorable = strong
            ? new[] { FengShuiCalculator.GetGeneratedElement(dayMaster), FengShuiCalculator.GetControlledElement(dayMaster) }
            : new[] { supporter, dayMaster };
        Assert.Empty(expected.Intersect(favorable));
        Assert.True(expected.Union(favorable).Count() >= 4);
    }

    [Fact(DisplayName = "NEED-36-08 [Boundary] Formula stamp is 3.6 and room flow keeps ĝ·p")]
    public void Version_And_RoomFlow_Untouched()
    {
        Assert.Equal("3.6", ScoringFormulaVersions.Current);

        // Luồng phòng: không có PersonalAvoid/needCover, gapScore vẫn là ĝ·p.
        var ctx = new ScoringContext
        {
            AdjustedIdeal = V(kim: .5m, tho: .5m),
            CurrentVector = ElementVector.Uniform,
            Scope = WorkspaceScope.Public,
            Purpose = WorkPurpose.Other,
            PersonalWeight = 0m,
            Params = Prms,
        };
        var scored = new RecommendationScorer().ScoreSingle(ctx, new ProductFacts(Guid.NewGuid(), V(kim: 1m), new HashSet<string>(), ProductPlacement.Living));
        Assert.Null(scored.Breakdown!.PersonalNeedCover);
        Assert.Null(scored.Breakdown!.PersonalAvoidElements);
        Assert.DoesNotContain(scored.Breakdown!.Components, c => c.Code == ScoreComponentCodes.PersonalAvoidScore);
    }
}
