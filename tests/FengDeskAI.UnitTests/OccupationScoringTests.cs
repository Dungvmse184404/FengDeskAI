using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// P5 — nghề nghiệp bẻ vector điểm quan hệ <c>r</c> (ADR v3.2 §11, phương án N1). Data-Driven: mỗi ca
/// là một <see cref="OccupationCase"/>, thêm ca = thêm một dòng dữ liệu.
///
/// <code>
/// r'[e] = clamp(r[e] + delta[e]·OCCUPATION_SHARE, −1, 1)
/// r'[e] = min(r'[e], −0.1)   khi GetRelation(mệnh, e) == BiKhac   // nghề không đổi được bản mệnh
/// d     = (1 − Wp)·ĝ + Wp·r'
/// </code>
///
/// <para>
/// Mọi ca dựng phòng có <c>adjustedIdeal ≡ current</c> ⇒ <c>gap = 0</c> ⇒ <c>ĝ = 0</c>, nên
/// <c>d·p = Wp·r'[e]</c> và kỳ vọng rút về đúng một số hạng. <see cref="ProductPlacement.Living"/>
/// tắt Directional Validation, <see cref="WorkPurpose.Other"/> tắt luật cảm hứng không gian.
/// </para>
///
/// <para>
/// Ca chạy trên MỌI bản mệnh: hành khắc và hành ưa suy ra từ luật ngũ hành chứ không hard-code, nên
/// bộ test không mục khi bảng <c>feng_shui_rules</c> đổi.
/// </para>
/// </summary>
public sealed class OccupationScoringTests
{
    private static readonly Guid ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private const decimal Wp = 0.50m;

    /// <summary>Phòng "phẳng": gap = 0 nên ĝ = 0, mọi thứ còn lại đến từ trục cá nhân.</summary>
    private static readonly ElementVector FlatRoom =
        new(Tho: 0.2m, Kim: 0.2m, Thuy: 0.2m, Moc: 0.2m, Hoa: 0.2m);

    /// <summary>Hành đầu tiên KHẮC bản mệnh — suy từ luật, không hard-code.</summary>
    private static FengShuiElement Clashing(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .First(e => FengShuiCalculator.GetRelation(destiny, e) == FengShuiRelation.BiKhac);

    /// <summary>Hành bản mệnh ƯA (không khắc, không phải chính nó) — nơi delta được phép tác động đủ.</summary>
    private static FengShuiElement Friendly(FengShuiElement destiny)
        => Enum.GetValues<FengShuiElement>()
            .First(e => e != destiny && FengShuiCalculator.GetRelation(destiny, e) != FengShuiRelation.BiKhac);

    // ===================== Bộ chạy chung =====================

    private static ScoringContext ContextOf(
        FengShuiElement destiny, ElementVector? delta, decimal share, ElementVector? personalNeed = null) => new()
    {
        AdjustedIdeal = FlatRoom,
        CurrentVector = FlatRoom,
        PersonalVector = ElementVector.Single(destiny),
        PersonalNeedVector = personalNeed,
        Scope = WorkspaceScope.Private,
        Purpose = WorkPurpose.Other,
        PersonalWeight = Wp,
        OccupationDelta = delta,
        OccupationCode = delta is null ? null : "IT",
        OccupationNameVi = delta is null ? null : "CNTT / Lập trình",
        Params = ScoringParameters.Default with { OccupationShare = share },
    };

    private static ScoredProduct Score(
        FengShuiElement destiny, FengShuiElement product, ElementVector? delta = null,
        decimal share = 0m, ProductPlacement placement = ProductPlacement.Living,
        ElementVector? personalNeed = null)
    {
        var facts = new ProductFacts(
            ProductId, ElementVector.Single(product), new HashSet<string>(), placement);
        var scored = new RecommendationScorer()
            .Score(ContextOf(destiny, delta, share, personalNeed), new[] { facts })
            .FirstOrDefault();
        Assert.NotNull(scored);
        return scored!;
    }

    // ===================== A. Bảng ca =====================

    [Theory(DisplayName = "SCORE-OCC")]
    [MemberData(nameof(Cases))]
    public void Occupation_BendsTheRuleScoreAsSpecified(OccupationCase c)
    {
        var friendly = Friendly(c.Destiny);
        var clashing = Clashing(c.Destiny);

        var delta = BuildDelta(friendly, clashing, c);
        var target = c.ProductIsClashing ? clashing : friendly;
        var baseline = Score(c.Destiny, target);
        var shifted = Score(c.Destiny, target, delta, c.Share);

        // (1) Điểm dịch đúng Wp × delta × share — tuyến tính, không nhảy bậc. Đây là lý do chọn N1
        //     thay vì bẻ `personalVector` (engine chỉ đọc `.Dominant()` nên delta nhỏ bị nuốt mất,
        //     delta lớn thì LẬT ĐỈNH — nhảy bậc).
        Assert.Equal(c.ExpectedScoreShift, Math.Round(shifted.Score - baseline.Score, 3));

        var breakdown = shifted.Breakdown!;

        // (2) Chỉ khai báo "có nghề nghiệp" khi nó DỊCH ĐƯỢC thật — nếu không FE vẽ một lớp radar
        //     phẳng lì kèm dòng giải thích rỗng nghĩa.
        if (!c.ExpectsOccupationReported)
        {
            Assert.Null(breakdown.OccupationShift);
            Assert.Null(breakdown.OccupationCode);
            return;
        }

        Assert.Equal("IT", breakdown.OccupationCode);
        Assert.Equal(c.Share, breakdown.OccupationShare);

        var shift = breakdown.OccupationShift!.Value;
        Assert.Equal(c.ExpectedFriendlyShift, Math.Round(shift[friendly], 3));

        // (3) `occupationShift` = r' − r, đo SAU khi chặn. Trục bị chặn hiện gần 0 chứ không hiện
        //     `delta·share` danh nghĩa — một lớp radar nói "nghề của bạn nâng Kim" trong khi Kim vẫn
        //     khắc mệnh là nói dối bằng đồ hoạ.
        Assert.Equal(
            Math.Round(breakdown.RuleScoreVector!.Value[clashing] - breakdown.BaseRuleScoreVector!.Value[clashing], 6),
            Math.Round(shift[clashing], 6));

        // (4) Nghề nghiệp KHÔNG đổi được bản mệnh: hành BiKhac bị chặn trần ở −0.1 dù delta tới đâu.
        Assert.True(breakdown.RuleScoreVector!.Value[clashing] <= -0.1m,
            $"{c.Id}: mệnh {c.Destiny}, hành khắc {clashing} — r' = {breakdown.RuleScoreVector.Value[clashing]}, phải ≤ −0.1.");

        // (5) Phạt kiêng kỵ là PHẠM TRÙ (đi đường GetRelation), nghề nghiệp không được chạm vào — §14.4.
        Assert.Equal(baseline.Breakdown!.UserPenalty, breakdown.UserPenalty);
    }

    private static ElementVector BuildDelta(
        FengShuiElement friendly, FengShuiElement clashing, OccupationCase c)
        => ElementVector.Single(friendly).Scale(c.DeltaOnFriendly)
            .Add(ElementVector.Single(clashing).Scale(c.DeltaOnClashing));

    public static TheoryData<OccupationCase> Cases()
    {
        var data = new TheoryData<OccupationCase>();

        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        {
            data.Add(new OccupationCase
            {
                Id = "SCORE-OCC-01", Name = "[Boundary] A zero share leaves every number untouched",
                Destiny = destiny, DeltaOnFriendly = 0.50m, DeltaOnClashing = -0.50m, Share = 0.00m,
                ExpectedScoreShift = 0.000m, ExpectsOccupationReported = false,
                Why = "Kill-switch: mọi delta × 0 ⇒ r' ≡ r ⇒ byte-identical như khi chưa có P5.",
            });

            data.Add(new OccupationCase
            {
                Id = "SCORE-OCC-02", Name = "[Boundary] An all-zero delta vector changes nothing",
                Destiny = destiny, DeltaOnFriendly = 0m, DeltaOnClashing = 0m, Share = 1.00m,
                ExpectedScoreShift = 0.000m, ExpectsOccupationReported = false,
                Why = "Nghề chưa được chuyên gia nhập delta ⇒ chỉ là nhãn hồ sơ, không được lén đổi thứ hạng.",
            });

            data.Add(new OccupationCase
            {
                Id = "SCORE-OCC-03", Name = "[Normal] The delta shifts the score by Wp × delta × share",
                Destiny = destiny, DeltaOnFriendly = 0.20m, Share = 1.00m,
                ExpectedScoreShift = 0.100m, ExpectedFriendlyShift = 0.200m,
                Why = "ĝ = 0 và sản phẩm thuần một hành ⇒ điểm dịch = Wp × delta × share = 0.5 × 0.20 × 1.0.",
            });

            data.Add(new OccupationCase
            {
                Id = "SCORE-OCC-04", Name = "[Normal] Half the share moves the score by half as much",
                Destiny = destiny, DeltaOnFriendly = 0.20m, Share = 0.50m,
                ExpectedScoreShift = 0.050m, ExpectedFriendlyShift = 0.100m,
                Why = "Tuyến tính theo share: 0.5 × 0.20 × 0.5 = 0.050.",
            });

            data.Add(new OccupationCase
            {
                Id = "SCORE-OCC-05", Name = "[Abnormal] A negative delta pushes the score down symmetrically",
                Destiny = destiny, DeltaOnFriendly = -0.20m, Share = 1.00m,
                ExpectedScoreShift = -0.100m, ExpectedFriendlyShift = -0.200m,
                Why = "Delta âm phải hạ điểm đúng bằng mức delta dương nâng lên — không có chiều nào ưu ái.",
            });

            data.Add(new OccupationCase
            {
                Id = "SCORE-L2-07", Name = "[Boundary] An occupation cannot lift a clashing element out of the negative",
                Destiny = destiny, DeltaOnFriendly = 0.20m, DeltaOnClashing = 1.00m, Share = 1.00m,
                ProductIsClashing = true,
                ExpectedScoreShift = 0.450m, ExpectedFriendlyShift = 0.200m,
                Why = "§14.7 (hoãn từ P1 sang P5): delta = +1.0 và share mở hết vẫn KHÔNG kéo nổi hành BiKhac "
                    + "lên ≥ 0 — trần chặn ở −0.1. r đi từ −1.0 lên đúng −0.1 nên điểm dịch "
                    + "0.5 × (−0.1 − (−1.0)) = 0.450, mà sản phẩm vẫn ở phe ÂM và vẫn bị phạt đủ như cũ: "
                    + "−0.800 → −0.350. Nghề đổi mức ƯA THÍCH, không đổi được bản mệnh.",
            });
        }

        return data;
    }

    // ===================== B. Ranh giới luồng =====================

    /// <summary>
    /// Luồng <see cref="ProductPlacement.Carry"/> chấm theo vector dụng thần, KHÔNG dựng <c>r</c> —
    /// nên N1 không có chỗ bám và nghề nghiệp không tác động. Khoá lại thành hành vi CÓ CHỦ Ý: muốn
    /// nghề vào luồng đó thì phải bẻ chính vector dụng thần, một quyết định nghiệp vụ khác chưa chốt (§11.2).
    /// </summary>
    [Theory(DisplayName = "SCORE-OCC-06 [Boundary] The carry flow is untouched by occupation")]
    [MemberData(nameof(Destinies))]
    public void Occupation_DoesNotAffectTheCarryFlow(FengShuiElement destiny)
    {
        var friendly = Friendly(destiny);
        var need = ElementVector.Single(friendly);

        decimal ScoreCarry(ElementVector? delta, decimal share)
            => Score(destiny, friendly, delta, share, ProductPlacement.Carry, need).Score;

        Assert.Equal(
            ScoreCarry(null, 0m),
            ScoreCarry(ElementVector.Single(friendly).Scale(0.5m), 1m));
    }

    public static TheoryData<FengShuiElement> Destinies()
    {
        var data = new TheoryData<FengShuiElement>();
        foreach (var destiny in Enum.GetValues<FengShuiElement>()) data.Add(destiny);
        return data;
    }
}

/// <summary>Một ca của P5 — xem <see cref="OccupationScoringTests"/>.</summary>
public sealed class OccupationCase
{
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    public FengShuiElement Destiny { get; init; }

    /// <summary>Delta trên hành bản mệnh ƯA — nơi nghề nghiệp được phép tác động đủ.</summary>
    public decimal DeltaOnFriendly { get; init; }

    /// <summary>Delta trên hành KHẮC bản mệnh — nơi engine phải chặn lại.</summary>
    public decimal DeltaOnClashing { get; init; }

    public decimal Share { get; init; }

    /// <summary>Sản phẩm thuần hành KHẮC mệnh thay vì hành ưa.</summary>
    public bool ProductIsClashing { get; init; }

    /// <summary>Mức điểm dịch kỳ vọng so với khi không có nghề.</summary>
    public decimal ExpectedScoreShift { get; init; }

    /// <summary><c>occupationShift[hành ưa]</c> kỳ vọng.</summary>
    public decimal ExpectedFriendlyShift { get; init; }

    /// <summary>Breakdown có phải khai báo khối <c>occupation</c> không.</summary>
    public bool ExpectsOccupationReported { get; init; } = true;

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Destiny} {Name}";
}
