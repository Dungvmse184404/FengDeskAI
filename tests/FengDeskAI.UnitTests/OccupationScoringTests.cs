using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// N3 — nghề nghiệp là TRỤC THỨ BA của hướng chấm điểm (ADR <c>occupation-product-fit-v1.md</c> §3).
///
/// <code>
/// δ  = profile − 0.2 ;  ô = δ / (|δ|₁/2) ;  ô[e] = min(ô[e], 0) khi e khắc mệnh
/// d  = (1 − Wp − Wo)·ĝ + Wp·r + Wo·ô        (phòng)
/// d  = (1 − Wo)·n̂ + Wo·ô                    (Carry)
/// </code>
///
/// <para>
/// Mọi ca phòng dựng <c>adjustedIdeal ≡ current</c> ⇒ <c>ĝ = 0</c>, nên phần phòng triệt tiêu và kỳ vọng
/// rút về <c>Wp·r[e] + Wo·ô[e]</c>. Hồ sơ dựng dạng <c>Uniform + w·Single(up) − w·Single(down)</c> ⇒
/// <c>ô[up] = +1, ô[down] = −1</c>, còn lại 0 — bất kể <c>w</c>, nên kỳ vọng không phụ thuộc số liệu nháp.
/// Ca chạy trên MỌI bản mệnh; hành khắc/hành ưa suy từ luật ngũ hành, không hard-code.
/// </para>
/// </summary>
public sealed class OccupationScoringTests
{
    private static readonly Guid ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const decimal Wp = 0.50m;
    private const decimal Wo = 0.20m;

    private static readonly ElementVector FlatRoom = ElementVector.Uniform;

    private static FengShuiElement Clashing(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .First(e => FengShuiCalculator.GetRelation(destiny, e) == FengShuiRelation.BiKhac);

    private static FengShuiElement Friendly(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .First(e => e != destiny && FengShuiCalculator.GetRelation(destiny, e) != FengShuiRelation.BiKhac);

    /// <summary>Một hành "trung lập": không phải mệnh, không khắc mệnh, không phải hành ưa đã chọn.</summary>
    private static FengShuiElement Neutral(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .Last(e => e != destiny && e != Clashing(destiny) && e != Friendly(destiny)
                       && FengShuiCalculator.GetRelation(destiny, e) != FengShuiRelation.BiKhac);

    /// <summary><c>Uniform + w·up − w·down</c> — Σ=1, không âm, và ô = Single(up) − Single(down).</summary>
    private static ElementVector ProfileFavoring(FengShuiElement up, FengShuiElement down, decimal w = 0.15m)
        => ElementVector.Uniform.Add(ElementVector.Single(up).Scale(w)).Subtract(ElementVector.Single(down).Scale(w));

    private static ScoringContext ContextOf(
        FengShuiElement? destiny, ElementVector? profile, decimal weight,
        ElementVector? personalNeed = null, WorkspaceScope scope = WorkspaceScope.Private) => new()
    {
        AdjustedIdeal = FlatRoom,
        CurrentVector = FlatRoom,
        PersonalVector = destiny is { } d ? ElementVector.Single(d) : null,
        PersonalNeedVector = personalNeed,
        Scope = scope,
        Purpose = WorkPurpose.Other,
        PersonalWeight = destiny is null ? 0m : Wp,
        OccupationProfile = profile,
        OccupationWeight = weight,
        OccupationCode = profile is null ? null : "IT",
        OccupationNameVi = profile is null ? null : "CNTT / Lập trình",
        Params = ScoringParameters.Default with { OccupationWeight = weight },
    };

    private static ScoredProduct Score(
        FengShuiElement? destiny, FengShuiElement product, ElementVector? profile = null,
        decimal weight = 0m, ProductPlacement placement = ProductPlacement.Living,
        ElementVector? personalNeed = null, WorkspaceScope scope = WorkspaceScope.Private)
    {
        var facts = new ProductFacts(ProductId, ElementVector.Single(product), new HashSet<string>(), placement);
        var scored = new RecommendationScorer()
            .Score(ContextOf(destiny, profile, weight, personalNeed, scope), new[] { facts })
            .FirstOrDefault();
        Assert.NotNull(scored);
        return scored!;
    }

    private static ScoreComponent? OccupationComponent(ScoredProduct s)
        => s.Breakdown!.Components.FirstOrDefault(c => c.Code == ScoreComponentCodes.OccupationScore);

    private static void AssertComponentsSumToBlended(ScoreBreakdown b)
        => Assert.Equal(Math.Round(b.Blended, 3), Math.Round(b.Components.Sum(c => c.Contribution), 3));

    public static TheoryData<FengShuiElement> Destinies()
    {
        var data = new TheoryData<FengShuiElement>();
        foreach (var destiny in Enum.GetValues<FengShuiElement>()) data.Add(destiny);
        return data;
    }

    // ===================== Kill-switch =====================

    [Theory(DisplayName = "OCC-N3-01 [Boundary] Weight 0 leaves every number untouched")]
    [MemberData(nameof(Destinies))]
    public void ZeroWeight_IsByteIdentical(FengShuiElement destiny)
    {
        var friendly = Friendly(destiny);
        var profile = ProfileFavoring(friendly, Neutral(destiny));

        var baseline = Score(destiny, friendly);
        var withOccupation = Score(destiny, friendly, profile, weight: 0m);

        Assert.Equal(baseline.Score, withOccupation.Score);
        Assert.Equal(baseline.Breakdown!.CombinedDirection, withOccupation.Breakdown!.CombinedDirection);
        Assert.Null(withOccupation.Breakdown.OccupationCode);
        Assert.Null(withOccupation.Breakdown.OccupationDirection);
        Assert.Null(OccupationComponent(withOccupation));
    }

    [Theory(DisplayName = "OCC-N3-02 [Boundary] A uniform profile (OTHER) turns the axis off even at full weight")]
    [MemberData(nameof(Destinies))]
    public void UniformProfile_TurnsAxisOff(FengShuiElement destiny)
    {
        var friendly = Friendly(destiny);

        var baseline = Score(destiny, friendly);
        var withOther = Score(destiny, friendly, ElementVector.Uniform, weight: 1m);

        Assert.Equal(baseline.Score, withOther.Score);
        Assert.Null(withOther.Breakdown!.OccupationCode);
        Assert.Null(OccupationComponent(withOther));
        Assert.Null(OccupationAxis.Build(ElementVector.Uniform, destiny, 1m, "OCCUPATION_WEIGHT", "OTHER", "Khác"));
    }

    // ===================== Luồng phòng =====================

    [Theory(DisplayName = "OCC-N3-03 [Normal] Workspace: three components, Σ contribution = blended, shift = Wo·ô")]
    [MemberData(nameof(Destinies))]
    public void Workspace_AddsThirdComponent(FengShuiElement destiny)
    {
        var friendly = Friendly(destiny);
        var profile = ProfileFavoring(friendly, Neutral(destiny));

        var baseline = Score(destiny, friendly);
        var shifted = Score(destiny, friendly, profile, Wo);

        // ĝ = 0, ô[friendly] = +1 ⇒ điểm dịch đúng Wo × 1. Trục cá nhân giữ nguyên Wp·r[friendly].
        Assert.Equal(Wo, Math.Round(shifted.Score - baseline.Score, 3));

        var b = shifted.Breakdown!;
        Assert.Equal(3, b.Components.Count);
        AssertComponentsSumToBlended(b);

        var gap = b.Components.Single(c => c.Code == ScoreComponentCodes.GapScore);
        var personal = b.Components.Single(c => c.Code == ScoreComponentCodes.PersonalScore);
        var occ = OccupationComponent(shifted)!;
        Assert.Equal(1m - Wp - Wo, gap.Weight);
        Assert.Equal(Wp, personal.Weight);
        Assert.Equal(Wo, occ.Weight);
        Assert.Equal(1m, occ.Value);
        Assert.Equal("IT", b.OccupationCode);
        Assert.Equal(Wo, b.OccupationWeight);
        Assert.Equal(ScoringParamCodes.OccupationWeight, b.OccupationWeightCode);
        Assert.Equal(ScoringFormulaVersions.Current, b.FormulaVersion);
    }

    [Theory(DisplayName = "OCC-N3-07 [Normal] No birthdate (Wp = 0): the occupation axis still runs")]
    [MemberData(nameof(Destinies))]
    public void NoBirthdate_AxisStillActive(FengShuiElement destiny)
    {
        // `destiny` chỉ dùng để chọn hành; context truyền null ⇒ không có mệnh, Wp = 0.
        var up = Friendly(destiny);
        var profile = ProfileFavoring(up, Neutral(destiny));

        var baseline = Score(null, up);
        var shifted = Score(null, up, profile, Wo);

        Assert.Equal(0m, baseline.Score);          // ĝ = 0, không mệnh ⇒ không có gì để chấm
        Assert.Equal(Wo, shifted.Score);           // chỉ còn Wo·ô[up] = Wo
        Assert.NotNull(OccupationComponent(shifted));
        Assert.Equal(2, shifted.Breakdown!.Components.Count);
        Assert.Equal(1m - Wo, shifted.Breakdown.Components.Single(c => c.Code == ScoreComponentCodes.GapScore).Weight);
        AssertComponentsSumToBlended(shifted.Breakdown);
    }

    [Theory(DisplayName = "OCC-N3-08 [Boundary] OCCUPATION_SCORE does not depend on workspace scope")]
    [MemberData(nameof(Destinies))]
    public void OccupationScore_IsScopeIndependent(FengShuiElement destiny)
    {
        var friendly = Friendly(destiny);
        var profile = ProfileFavoring(friendly, Neutral(destiny));

        var values = new[] { WorkspaceScope.Private, WorkspaceScope.Shared, WorkspaceScope.Public }
            .Select(scope => OccupationComponent(Score(destiny, friendly, profile, Wo, scope: scope))!.Value)
            .Distinct()
            .ToList();

        Assert.Single(values);
    }

    // ===================== Luồng Carry =====================

    [Theory(DisplayName = "OCC-N3-04 [Normal] Carry: two components, Σ contribution = blended")]
    [MemberData(nameof(Destinies))]
    public void Carry_AddsOccupationComponent(FengShuiElement destiny)
    {
        var friendly = Friendly(destiny);
        var neutral = Neutral(destiny);
        var need = ElementVector.Single(friendly);
        var profile = ProfileFavoring(friendly, neutral);   // ô[neutral] = −1

        // Sản phẩm thuần hành trung lập: n̂·p = 0, ô·p = −1 ⇒ score = (1−Wo)·0 + Wo·(−1) = −Wo.
        var baseline = Score(destiny, neutral, placement: ProductPlacement.Carry, personalNeed: need);
        var shifted = Score(destiny, neutral, profile, Wo, ProductPlacement.Carry, need);

        Assert.Equal(0m, baseline.Score);
        Assert.Equal(-Wo, shifted.Score);

        var b = shifted.Breakdown!;
        Assert.Equal(ScoringTarget.PersonalNeed, b.Target);
        Assert.Equal(2, b.Components.Count);
        AssertComponentsSumToBlended(b);
        Assert.Equal(1m - Wo, b.Components.Single(c => c.Code == ScoreComponentCodes.PersonalNeedScore).Weight);
        Assert.Equal(-1m, OccupationComponent(shifted)!.Value);
    }

    // ===================== Kiêng kỵ =====================

    [Theory(DisplayName = "OCC-N3-05 [Boundary] A clashing element is clamped to ≤ 0, raw direction kept for display")]
    [MemberData(nameof(Destinies))]
    public void ClashingElement_IsClamped(FengShuiElement destiny)
    {
        var clashing = Clashing(destiny);
        var profile = ProfileFavoring(clashing, Neutral(destiny));

        var axis = OccupationAxis.Build(profile, destiny, Wo, "OCCUPATION_WEIGHT", "FINANCE", "Tài chính")!;

        Assert.Equal(1m, axis.RawDirection[clashing]);
        Assert.Equal(0m, axis.Direction[clashing]);
        Assert.True(axis.WasClamped);
        Assert.Equal(new[] { clashing }, axis.ClampedElements);

        // Không biết mệnh (mặt A) ⇒ không chặn.
        var anonymous = OccupationAxis.Build(profile, null, Wo, "OCCUPATION_WEIGHT", "FINANCE", "Tài chính")!;
        Assert.Equal(anonymous.RawDirection, anonymous.Direction);

        // Mọi hành BiKhac của mọi mệnh đều ≤ 0 sau chặn, bất kể hồ sơ trỏ vào đâu.
        foreach (var e in Enum.GetValues<FengShuiElement>())
            if (FengShuiCalculator.GetRelation(destiny, e) == FengShuiRelation.BiKhac)
                Assert.True(axis.Direction[e] <= 0m, $"mệnh {destiny}, hành khắc {e} phải ≤ 0");
    }

    [Theory(DisplayName = "OCC-N3-10 [Boundary] Occupation neither lifts a clashing product nor touches its penalty")]
    [MemberData(nameof(Destinies))]
    public void ClashingProduct_ScoreAndPenaltyUnchanged(FengShuiElement destiny)
    {
        var clashing = Clashing(destiny);
        var profile = ProfileFavoring(clashing, Neutral(destiny));

        var baseline = Score(destiny, clashing);
        var shifted = Score(destiny, clashing, profile, Wo);

        // ô[clashing] bị chặn về 0 ⇒ Wo·ô·p = 0 ⇒ điểm không dịch một chữ số; phạt đi đường GetRelation.
        Assert.Equal(baseline.Score, shifted.Score);
        Assert.Equal(baseline.Breakdown!.UserPenalty, shifted.Breakdown!.UserPenalty);
        Assert.Equal(0m, OccupationComponent(shifted)!.Value);
        Assert.Contains("khắc bản mệnh", OccupationComponent(shifted)!.ReasonVi);
    }

    // ===================== Trọng số & mặt A =====================

    [Fact(DisplayName = "OCC-N3-06 [Boundary] Wo is clamped so that Wp + Wo ≤ 1")]
    public void OccupationWeight_IsClampedAgainstPersonalWeight()
    {
        var p = ScoringParameters.Default with { OccupationWeight = 0.30m };

        Assert.Equal(0.30m, p.OccupationWeightFor(personalWeight: 0.50m));
        Assert.Equal(0.10m, p.OccupationWeightFor(personalWeight: 0.90m));
        Assert.Equal(0.00m, p.OccupationWeightFor(personalWeight: 1.00m));
        Assert.Equal(0.30m, p.OccupationWeightFor(personalWeight: 0.00m));
    }

    [Fact(DisplayName = "OCC-N3-09 [Normal] Product page: FINANCE × pure Kim = 0.750 → 88%, × pure Hỏa = −0.375 → 31%")]
    public void ProductPage_FinanceExample()
    {
        var finance = new ElementVector(Tho: 0.10m, Kim: 0.50m, Thuy: 0.30m, Moc: 0.05m, Hoa: 0.05m);
        var axis = OccupationAxis.Build(finance, null, 1m, "OCCUPATION_WEIGHT", "FINANCE", "Tài chính")!;

        Assert.Equal(0.75m, axis.RawDirection[FengShuiElement.Kim]);
        Assert.Equal(0.25m, axis.RawDirection[FengShuiElement.Thuy]);
        Assert.Equal(-0.25m, axis.RawDirection[FengShuiElement.Tho]);
        Assert.Equal(-0.375m, axis.RawDirection[FengShuiElement.Hoa]);
        Assert.Equal(-0.375m, axis.RawDirection[FengShuiElement.Moc]);

        decimal kim = axis.RawDirection.Dot(ElementVector.Single(FengShuiElement.Kim));
        decimal hoa = axis.RawDirection.Dot(ElementVector.Single(FengShuiElement.Hoa));
        Assert.Equal(88, ScoreBreakdownMapping.DisplayPercentOf(kim));
        Assert.Equal(31, ScoreBreakdownMapping.DisplayPercentOf(hoa));
    }

    [Fact(DisplayName = "OCC-N3-11 [Abnormal] Profile validation rejects Σ ≠ 1, duplicates and out-of-range shares")]
    public void ProfileRules_RejectInvalidProfiles()
    {
        var ok = new List<(FengShuiElement, decimal)>
        {
            (FengShuiElement.Kim, 0.50m), (FengShuiElement.Thuy, 0.30m), (FengShuiElement.Tho, 0.10m),
            (FengShuiElement.Hoa, 0.05m), (FengShuiElement.Moc, 0.05m),
        };
        Assert.Null(OccupationProfileRules.Validate(ok));
        Assert.Null(OccupationProfileRules.Validate(Array.Empty<(FengShuiElement, decimal)>()));

        Assert.NotNull(OccupationProfileRules.Validate(new[] { (FengShuiElement.Kim, 0.60m), (FengShuiElement.Thuy, 0.30m) }));
        Assert.NotNull(OccupationProfileRules.Validate(new[] { (FengShuiElement.Kim, 0.50m), (FengShuiElement.Kim, 0.50m) }));
        Assert.NotNull(OccupationProfileRules.Validate(new[] { (FengShuiElement.Kim, 1.20m), (FengShuiElement.Thuy, -0.20m) }));

        // Trong dung sai 0.001 thì chấp nhận — 5 số numeric(4,3) không luôn cộng đúng 1.000.
        Assert.Null(OccupationProfileRules.Validate(new[]
        {
            (FengShuiElement.Kim, 0.333m), (FengShuiElement.Thuy, 0.333m), (FengShuiElement.Tho, 0.333m),
        }));
    }

    [Theory(DisplayName = "OCC-N3-12 [Boundary] Every breakdown is stamped with the current formula (≥ 3.4)")]
    [MemberData(nameof(Destinies))]
    public void Breakdown_IsStampedWithCurrentFormula(FengShuiElement destiny)
    {
        // N3 vào từ 3.4; các bản sau (3.5 cap phiếu tag…) vẫn phải mang N3, nên so ≥ chứ không đóng đinh.
        Assert.Equal(ScoringFormulaVersions.Current, Score(destiny, Friendly(destiny)).Breakdown!.FormulaVersion);
        Assert.True(string.CompareOrdinal(ScoringFormulaVersions.Current, ScoringFormulaVersions.V34) >= 0);
    }
}
