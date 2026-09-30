using System;
using System.Collections.Generic;
using System.Linq;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// v3.7 — nhánh phòng (ADR <c>workspace-gap-cover-v3.7.md</c>): điểm đo "phủ nhu cầu" thay tích trong.
///
/// <code>
/// gapCover = Σ_e min(ĝ⁺[e], p[e])      gapOver = Σ_e min(ĝ⁻[e], p[e])
/// gapScore = clamp(gapCover − gapOver, −1, 1)
/// </code>
///
/// <para>
/// Lý do: <c>Σ ĝ⁺ = 1</c> và <c>Σ p = 1</c> ⇒ <c>ĝ·p</c> LÀ một phép trung bình có trọng số, mà trung bình
/// không vượt được phần tử lớn nhất ⇒ trần của sản phẩm bằng chính <c>max p</c>. Sản phẩm khai đủ hành bị
/// phạt so với sản phẩm khai thuần một hành, dù hợp phòng hơn.
/// </para>
///
/// <para>
/// Mọi ca dựng phòng bằng cách cho thẳng <c>adjustedIdeal</c> và <c>current</c> sao cho <c>|gap|₁ = 2</c> —
/// khi đó <c>ĝ = gap</c>, số trong đầu bài đọc ra luôn được, không phải nhẩm bước chuẩn hoá.
/// </para>
/// </summary>
public sealed class WorkspaceGapCoverV37Tests
{
    private static readonly ScoringParameters Prms = ScoringParameters.Default with { OccupationWeight = 0m };

    private static ElementVector V(decimal kim = 0, decimal moc = 0, decimal thuy = 0, decimal hoa = 0, decimal tho = 0)
        => new(Tho: tho, Kim: kim, Thuy: thuy, Moc: moc, Hoa: hoa);

    /// <summary>
    /// Phòng không có trục cá nhân lẫn trục nghề ⇒ <c>blended = gapScore</c>, và <c>Living</c> không chấm
    /// hướng ⇒ không có penalty nào. Điểm cuối chính là số cần kiểm.
    /// </summary>
    private static ScoredProduct Score(ElementVector ideal, ElementVector current, ElementVector product)
        => new RecommendationScorer().ScoreSingle(
            new ScoringContext
            {
                AdjustedIdeal = ideal,
                CurrentVector = current,
                Scope = WorkspaceScope.Public,
                Purpose = WorkPurpose.Other,
                PersonalWeight = 0m,
                Params = Prms,
            },
            new ProductFacts(Guid.NewGuid(), product, new HashSet<string>(), ProductPlacement.Living));

    // Phòng của ADR §1.1: thiếu Kim .5 / Thủy .3 / Mộc .2, thừa Hỏa .6 / Thổ .4.
    private static readonly ElementVector RoomIdeal = V(kim: .5m, thuy: .3m, moc: .2m);
    private static readonly ElementVector RoomCurrent = V(hoa: .6m, tho: .4m);

    [Fact(DisplayName = "GAP-37-01 [Normal] A fully declared product is no longer capped by its own peak")]
    public void FullyDeclaredProduct_ScoresByCoverage_NotByItsOwnPeak()
    {
        // "Cây Phát Tài Thủy Sinh" trên production: Kim .44 / Thủy .36 / Mộc .20.
        var product = V(kim: .44m, thuy: .36m, moc: .20m);

        // cover = min(.5,.44) + min(.3,.36) + min(.2,.20) = .44 + .30 + .20 = .94 ; over = 0.
        Assert.Equal(0.940m, Score(RoomIdeal, RoomCurrent, product).Score);

        // Trước v3.7: ĝ·p = .5×.44 + .3×.36 + .2×.20 = 0.368 — đây là phòng HỢP NHẤT có thể có với sản
        // phẩm này, vậy mà trần của nó vẫn chỉ là đỉnh khai báo (0.44). Đó là lỗi cần sửa.
        var ghat = RoomIdeal.Subtract(RoomCurrent);
        Assert.Equal(0.368m, Math.Round(ghat.Dot(product), 3));
    }

    [Theory(DisplayName = "GAP-37-02 [Boundary] A single-element product keeps its v3.6 score exactly")]
    [InlineData(FengShuiElement.Kim)]
    [InlineData(FengShuiElement.Moc)]
    [InlineData(FengShuiElement.Thuy)]
    [InlineData(FengShuiElement.Hoa)]
    [InlineData(FengShuiElement.Tho)]
    public void SingleElementProduct_MatchesTheOldDotProduct(FengShuiElement element)
    {
        // Với p = e_k: cover = ĝ⁺[k], over = ĝ⁻[k], một trong hai luôn bằng 0 ⇒ hiệu = ĝ[k] = ĝ·p.
        // Đây là bất biến khiến v3.7 KHÔNG dịch thang điểm: 4 sản phẩm khai thuần một hành trên prod
        // giữ nguyên điểm, phần thay đổi chỉ rơi vào nhóm khai đủ hành.
        var product = ElementVector.Single(element);
        var ghat = RoomIdeal.Subtract(RoomCurrent);

        Assert.Equal(Math.Round(ghat.Dot(product), 3), Score(RoomIdeal, RoomCurrent, product).Score);
    }

    [Fact(DisplayName = "GAP-37-03 [Boundary] Covering the room's needs exactly reaches plus one")]
    public void ProductEqualToTheDeficit_ReachesPlusOne()
    {
        // p ≡ ĝ⁺ ⇒ cover = Σ ĝ⁺ = 1, over = 0. Trước v3.7 ca này chỉ được ĝ·p = .25+.09+.04 = 0.38.
        Assert.Equal(1.000m, Score(RoomIdeal, RoomCurrent, V(kim: .5m, thuy: .3m, moc: .2m)).Score);
    }

    [Fact(DisplayName = "GAP-37-04 [Boundary] Pouring only into what the room already has reaches minus one")]
    public void ProductEqualToTheSurplus_ReachesMinusOne()
    {
        // p ≡ ĝ⁻ ⇒ cover = 0, over = 1.
        Assert.Equal(-1.000m, Score(RoomIdeal, RoomCurrent, V(hoa: .6m, tho: .4m)).Score);
    }

    [Fact(DisplayName = "GAP-37-05 [Normal] Overfill is capped per element, not summed over the product")]
    public void Overfill_IsCappedPerElement()
    {
        // Phía trừ dùng min(ĝ⁻, p) chứ không phải Σ p như AvoidHit bên Carry: "phòng đã thừa" là một
        // MỨC ĐỘ. Ở đây p = Hỏa .5 / Thổ .5 ⇒ over = min(.6,.5) + min(.4,.5) = .5 + .4 = 0.9.
        Assert.Equal(-0.900m, Score(RoomIdeal, RoomCurrent, V(hoa: .5m, tho: .5m)).Score);
        Assert.Equal(0.900m, Score(RoomIdeal, RoomCurrent, V(hoa: .5m, tho: .5m)).Breakdown!.GapOverfill);

        // Nếu phía trừ cộng thẳng Σ p (kiểu AvoidHit) thì số trên đã là 1.0 và điểm là −1.000.
        Assert.NotEqual(-1.000m, Score(RoomIdeal, RoomCurrent, V(hoa: .5m, tho: .5m)).Score);
    }

    [Fact(DisplayName = "GAP-37-06 [Normal] The breakdown exposes both halves and still adds up")]
    public void Breakdown_ExposesCoverAndOverfill_AndStillAddsUp()
    {
        var product = V(kim: .44m, thuy: .36m, hoa: .20m);
        var b = Score(RoomIdeal, RoomCurrent, product).Breakdown!;

        Assert.Equal(ScoringFormulaVersions.Current, b.FormulaVersion);
        Assert.NotNull(b.GapCover);
        Assert.NotNull(b.GapOverfill);
        Assert.Equal(b.GapScore, b.GapCover!.Value - b.GapOverfill!.Value);

        // cover = min(.5,.44) + min(.3,.36) = .74 ; over = min(.6,.20) = .20 ⇒ 0.540.
        Assert.Equal(0.740m, b.GapCover!.Value);
        Assert.Equal(0.200m, b.GapOverfill!.Value);

        // Bất biến duy nhất user đọc được trên ScoreWaterfall: Σ contribution = blended.
        Assert.Equal(b.Blended, b.Components.Sum(c => c.Contribution));

        // Câu giải thích phải kể ĐÚNG phép tính đang chạy — "phủ", không phải "ĝ nhân p".
        var gapRow = b.Components.Single(c => c.Code == ScoreComponentCodes.GapScore);
        Assert.Contains("Đáp ứng", gapRow.ReasonVi);
        Assert.Contains("phòng đã thừa", gapRow.ReasonVi);
    }

    [Fact(DisplayName = "GAP-37-08 [Boundary] p·d no longer reproduces blended for a multi-element product")]
    public void CombinedDirection_IsNoLongerAReplacementForTheComponentList()
    {
        // ScoreBreakdownTests.AssertConsistent khoá "p·d ≈ blended" trên MA TRẬN 500+ tổ hợp, nhưng cả ma
        // trận đó dùng ElementVector.Single(...) — sản phẩm thuần một hành. Với sản phẩm khai đủ hành, số
        // hạng phòng không còn là tích trong nên đẳng thức đó SAI. Ghim sự thật này lại ở đây, thay vì để
        // nó nằm chờ người sau tưởng radar dựng lại được điểm.
        var product = V(kim: .44m, thuy: .36m, moc: .20m);
        var b = Score(RoomIdeal, RoomCurrent, product).Breakdown!;

        Assert.Equal(b.Blended, b.Components.Sum(c => c.Contribution));      // vẫn đúng — đường user đọc
        Assert.NotEqual(b.Blended, b.ProductVector.Dot(b.CombinedDirection)); // không còn đúng

        // CombinedDirection vẫn là vector radar hợp lệ ("đang ưu tiên bù hành nào"), chỉ thôi làm phép tính.
        Assert.Equal(RoomIdeal.Subtract(RoomCurrent), b.CombinedDirection);
    }

    [Fact(DisplayName = "GAP-37-07 [Abnormal] A room with nothing missing scores every product at zero or below")]
    public void RoomWithNoDeficit_NeverScoresPositive()
    {
        // current ≡ adjustedIdeal ⇒ gap = 0 ⇒ ĝ = 0 ⇒ cover = over = 0. Không có phép chia cho 0 nào.
        var same = V(kim: .2m, moc: .2m, thuy: .2m, hoa: .2m, tho: .2m);
        foreach (var element in Enum.GetValues<FengShuiElement>())
            Assert.Equal(0.000m, Score(same, same, ElementVector.Single(element)).Score);
    }

    // ── Trục nghề — cùng phép đo, vì `ô` vốn được dựng ĐÚNG THEO `ĝ` ─────────────────────────────

    /// <summary>
    /// Hồ sơ nghề cho ra đúng <c>ô</c> mà ADR <c>occupation-product-fit-v1</c> §2.2 ghi cho `FINANCE`:
    /// Kim +0.75 · Thủy +0.25 · Thổ −0.25 · Hỏa −0.375 · Mộc −0.375.
    /// (<c>δ = profile − 0.2</c> ⇒ Kim .30 / Thủy .10 / Thổ −.10 / Hỏa −.15 / Mộc −.15, <c>|δ|₁/2 = 0.4</c>.)
    /// </summary>
    private static readonly ElementVector FinanceProfile = V(kim: .50m, thuy: .30m, tho: .10m, hoa: .05m, moc: .05m);

    /// <summary>
    /// Phòng không thiếu gì (gap = 0) và <c>Wo = 1</c> ⇒ <c>blended</c> chính là số hạng nghề, đọc thẳng
    /// ra điểm cuối. Không khai bản mệnh ⇒ không clamp ⇒ <c>Direction == RawDirection</c>, tức đây cũng
    /// đúng là con số mặt A (chip "Hợp nghề X%" ở trang sản phẩm).
    /// </summary>
    private static ScoredProduct ScoreByOccupation(ElementVector product)
        => new RecommendationScorer().ScoreSingle(
            new ScoringContext
            {
                AdjustedIdeal = ElementVector.Uniform,
                CurrentVector = ElementVector.Uniform,
                Scope = WorkspaceScope.Public,
                Purpose = WorkPurpose.Other,
                PersonalWeight = 0m,
                OccupationProfile = FinanceProfile,
                OccupationWeight = 1m,
                OccupationCode = "FINANCE",
                OccupationNameVi = "Tài chính",
                Params = Prms,
            },
            new ProductFacts(Guid.NewGuid(), product, new HashSet<string>(), ProductPlacement.Living));

    [Fact(DisplayName = "OCC-37-01 [Boundary] A single-element product keeps the number the ADR documents")]
    public void Occupation_SingleElementProduct_IsUnchanged()
    {
        // ADR occupation-product-fit-v1 §2.2: sản phẩm 100% Kim ⇒ ô·p = 0.75 ⇒ 88%. Con số đó đã in ra
        // tài liệu và đang hiện trên chip trang sản phẩm — v3.7 không được phép làm nó xê dịch.
        Assert.Equal(0.750m, ScoreByOccupation(ElementVector.Single(FengShuiElement.Kim)).Score);
        Assert.Equal(-0.375m, ScoreByOccupation(ElementVector.Single(FengShuiElement.Hoa)).Score);
    }

    [Fact(DisplayName = "OCC-37-02 [Normal] A fully declared product is no longer averaged down")]
    public void Occupation_FullyDeclaredProduct_IsNoLongerAveragedDown()
    {
        // Kim .5 / Thủy .5 — mang ĐÚNG hai hành nghề Tài chính cần, không mang hành nào nghề nên tránh.
        // Nay: min(.75,.5) + min(.25,.5) = .50 + .25 = 0.75 — bằng sản phẩm thuần Kim, đúng trực giác.
        // Trước:  .75×.5 + .25×.5 = 0.500 (trung bình kéo xuống dù không có gì sai với sản phẩm).
        var product = V(kim: .5m, thuy: .5m);
        Assert.Equal(0.750m, ScoreByOccupation(product).Score);

        var axis = OccupationAxis.Build(
            FinanceProfile, destiny: null, weight: 1m,
            ScoringParamCodes.OccupationWeight, "FINANCE", "Tài chính")!;
        Assert.Equal(0.500m, Math.Round(axis.Direction.Dot(product), 3));
    }

    [Fact(DisplayName = "OCC-37-03 [Normal] The occupation row exposes both halves and adds up")]
    public void Occupation_BreakdownExposesCoverAndOverfill()
    {
        // Kim .4 / Hỏa .6 — nửa hợp nghề, nửa rơi vào hành nghề nên tránh.
        // cover = min(.75,.4) = .40 ; over = min(.375,.6) = .375 ⇒ 0.025.
        var b = ScoreByOccupation(V(kim: .4m, hoa: .6m)).Breakdown!;

        Assert.Equal(0.400m, b.OccupationCover);
        Assert.Equal(0.375m, b.OccupationOverfill);

        var row = b.Components.Single(c => c.Code == ScoreComponentCodes.OccupationScore);
        Assert.Equal(b.OccupationCover!.Value - b.OccupationOverfill!.Value, row.Value);
        Assert.Equal(b.Blended, b.Components.Sum(c => c.Contribution));
        Assert.Contains("nghề Tài chính", row.ReasonVi);
    }
}
