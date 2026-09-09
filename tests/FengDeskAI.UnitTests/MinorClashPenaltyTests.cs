using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// §18 — phạt phần hành KHẮC bản mệnh <b>không phải hành trội</b> của vật mang theo người.
/// Data-Driven: mỗi ca là một <see cref="MinorClashCase"/>, thêm ca = thêm một dòng dữ liệu.
///
/// <para>
/// Lỗ hổng đang bịt, dựng lại từ ca thật phát hiện trên UI: "Vòng tay thạch anh tím" cho người mệnh
/// Kim. Vector <c>Kim 0.50 · Thủy 0.30 · Hỏa 0.20</c>, mà Hỏa khắc Kim. Trước §18 nó lọt qua CẢ HAI:
/// </para>
/// <list type="number">
/// <item>Bộ lọc xung khắc chỉ so hành TRỘI — trội là Kim, tỷ hòa với mệnh, không bị chặn.</item>
/// <item>Vector dụng thần Σ=1 và KHÔNG ÂM — <c>dụngThần[Hỏa] = 0</c> nên 20% Hỏa nhân ra đúng 0.</item>
/// </list>
/// <para>
/// Ra 0.200 → 60% trên màn hình, không dấu vết nào của phần khắc mệnh. Chính là khiếm khuyết §10.2b
/// đã ghi cho <c>personalTarget</c>: luồng phòng được sửa ở v3.2 bằng <c>r</c> có dấu, luồng Carry
/// thì bị bỏ sót.
/// </para>
///
/// <para>Kỳ vọng tính TAY từ công thức trong ADR §18.3, không lấy ngược từ code:</para>
/// <code>
/// clashShare  = Σ product[e]  với mọi e mà GetRelation(mệnh, e) == BiKhac
/// userPenalty = MINOR_CLASH_PENALTY × clashShare
/// </code>
/// </summary>
public sealed class MinorClashPenaltyTests
{
    private static readonly Guid ProductId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>Vòng tay thạch anh tím: Crystal (Thủy/Kim) + tím (Hỏa) + tròn (Kim).</summary>
    private static readonly ElementVector Bracelet =
        new(Tho: 0m, Kim: 0.50m, Thuy: 0.30m, Moc: 0m, Hoa: 0.20m);

    /// <summary>Dụng thần của người mệnh Kim: Thổ sinh Kim là chính, Kim tỷ hòa là phụ.</summary>
    private static readonly ElementVector NeedOfKim =
        new(Tho: 0.60m, Kim: 0.40m, Thuy: 0m, Moc: 0m, Hoa: 0m);

    /// <summary>Phòng phẳng: gap = 0 nên ĝ = 0, điểm chỉ còn phần trục cá nhân.</summary>
    private static readonly ElementVector FlatRoom =
        new(Tho: 0.2m, Kim: 0.2m, Thuy: 0.2m, Moc: 0.2m, Hoa: 0.2m);

    // ===================== Bộ chạy chung =====================

    private static ScoringContext ContextOf(MinorClashCase c) => new()
    {
        PersonalVector = ElementVector.Single(c.Destiny),
        PersonalNeedVector = c.Placement == ProductPlacement.Carry ? NeedOfKim : null,
        AdjustedIdeal = c.Placement == ProductPlacement.Carry ? ElementVector.Zero : FlatRoom,
        CurrentVector = c.Placement == ProductPlacement.Carry ? ElementVector.Zero : FlatRoom,
        Scope = WorkspaceScope.Private,
        Purpose = WorkPurpose.Other,
        PersonalWeight = c.Wp,
        Params = ScoringParameters.Default with { MinorClashPenalty = c.MinorClashPenalty },
    };

    private static ScoredProduct? Run(MinorClashCase c)
        => new RecommendationScorer()
            .Score(ContextOf(c), new[] { new ProductFacts(ProductId, c.Product, new HashSet<string>(), c.Placement) })
            .FirstOrDefault();

    // ===================== A. Điểm số + dòng giải thích =====================

    [Theory(DisplayName = "SCORE-MC")]
    [MemberData(nameof(Cases))]
    public void MinorClashPenalty_ProducesExpectedScoreAndExplanation(MinorClashCase c)
    {
        var actual = Run(c);

        if (c.Expected is null)
        {
            Assert.True(actual is null, $"{c.Id}: kỳ vọng BỊ LOẠI khỏi danh sách. {c.Why}");
            return;
        }

        Assert.True(actual is not null, $"{c.Id}: kỳ vọng còn trong danh sách với điểm {c.Expected}. {c.Why}");
        Assert.Equal(c.Expected.Value, actual!.Score);

        var minorRow = actual.Breakdown!.Penalties
            .SingleOrDefault(p => p.Code == ScoringParamCodes.MinorClashPenalty);

        if (c.ExpectedMinorPenalty is null)
        {
            Assert.True(minorRow is null || !minorRow.Applied,
                $"{c.Id}: KHÔNG được có dòng phạt phần phụ. {c.Why}");
            return;
        }

        Assert.True(minorRow is not null, $"{c.Id}: thiếu dòng phạt {ScoringParamCodes.MinorClashPenalty}. {c.Why}");
        Assert.True(minorRow!.Applied, $"{c.Id}: dòng phạt phải được áp. {c.Why}");
        Assert.Equal(c.ExpectedMinorPenalty.Value, minorRow.Value);

        // Im lặng trừ điểm còn tệ hơn không trừ — dòng này là chỗ DUY NHẤT user đọc được lý do.
        foreach (var fragment in c.ReasonContains)
            Assert.Contains(fragment, minorRow.ReasonVi);
        Assert.Contains("phần phụ", minorRow.LabelVi);
    }

    public static TheoryData<MinorClashCase> Cases()
    {
        var data = new TheoryData<MinorClashCase>();

        // ── Ca thật đo được trên UI ──
        data.Add(new MinorClashCase
        {
            Id = "SCORE-MC-01", Name = "[Normal] A carry item is penalised for the clashing share it still carries",
            Destiny = FengShuiElement.Kim, Product = Bracelet, Placement = ProductPlacement.Carry,
            MinorClashPenalty = 0.60m,
            Expected = 0.080m, ExpectedMinorPenalty = 0.120m,
            ReasonContains = new[] { "Hỏa", "20" },
            Why = "dụngThần·sảnPhẩm = 0.6×0 + 0.4×0.50 = 0.200 (đo được trên UI: 60%); "
                + "clashShare = product[Hỏa] = 0.20 ⇒ phạt 0.60×0.20 = 0.120 ⇒ 0.080.",
        });

        data.Add(new MinorClashCase
        {
            Id = "SCORE-MC-02", Name = "[Boundary] Turning the penalty off restores the pre-§18 score exactly",
            Destiny = FengShuiElement.Kim, Product = Bracelet, Placement = ProductPlacement.Carry,
            MinorClashPenalty = 0.00m,
            Expected = 0.200m, ExpectedMinorPenalty = null,
            Why = "Kill-switch: 0 × clashShare = 0 ⇒ đúng con số trước §18, không lệch chữ số nào.",
        });

        // ── Ranh giới: hành TRỘI khắc mệnh đi đường cũ, không rơi vào công thức tỉ trọng ──
        data.Add(new MinorClashCase
        {
            Id = "SCORE-MC-03", Name = "[Boundary] A dominant clash is removed outright, not merely scaled",
            Destiny = FengShuiElement.Kim, Placement = ProductPlacement.Carry,
            Product = new ElementVector(Tho: 0m, Kim: 0.30m, Thuy: 0m, Moc: 0m, Hoa: 0.70m),
            MinorClashPenalty = 0.60m,
            Expected = null,
            Why = "Trội = Hỏa, khắc mệnh Kim ⇒ AlwaysHard loại thẳng. Nếu chuyển sang tỉ trọng thì vật "
                + "70% Hỏa chỉ còn bị trừ 0.42 và VẪN nằm trong danh sách — nới lỏng đúng nhóm cần phạt nặng nhất.",
        });

        data.Add(new MinorClashCase
        {
            Id = "SCORE-MC-04", Name = "[Abnormal] A carry item with no clashing element keeps the plain reason",
            Destiny = FengShuiElement.Kim, Placement = ProductPlacement.Carry,
            Product = new ElementVector(Tho: 0.50m, Kim: 0.50m, Thuy: 0m, Moc: 0m, Hoa: 0m),
            MinorClashPenalty = 0.60m,
            Expected = 0.500m, ExpectedMinorPenalty = null,
            Why = "Không hành nào khắc Kim ⇒ clashShare = 0 ⇒ không phạt. "
                + "Điểm = 0.6×0.50 + 0.4×0.50 = 0.500.",
        });

        // ── Ranh giới: luồng phòng KHÔNG đụng tới, vì r có dấu đã trừ phần khắc rồi ──
        data.Add(new MinorClashCase
        {
            Id = "SCORE-MC-05", Name = "[Boundary] The room flow prices the clash through signed r, so it is untouched",
            Destiny = FengShuiElement.Kim, Product = Bracelet, Placement = ProductPlacement.Living,
            Wp = 0.50m, MinorClashPenalty = 0.60m,
            Expected = 0.120m, ExpectedMinorPenalty = null,
            Why = "Phòng phẳng ⇒ ĝ = 0 ⇒ d·p = Wp·(r·p) = 0.5×(1.00×0.50 + (−0.20)×0.30 + (−1.00)×0.20) = 0.120. "
                + "Phần Hỏa đã bị r trừ; cộng thêm MINOR_CLASH_PENALTY nữa là đếm hai lần cùng một hành.",
        });

        return data;
    }

    // ===================== B. Kill-switch quét toàn bộ cặp (mệnh × hành) =====================

    /// <summary>
    /// <c>MINOR_CLASH_PENALTY = 0</c> không được để lọt một dòng phạt nào, với mọi cặp mệnh × hành —
    /// điều kiện để merge §18 mà không phải dựng lại kỳ vọng cũ ở đâu hết.
    /// </summary>
    [Theory(DisplayName = "SCORE-MC-06 [Boundary] A zero penalty leaves no minor-clash row anywhere")]
    [MemberData(nameof(DestinyElementPairs))]
    public void MinorClashPenalty_WhenZero_NeverAddsAPenaltyRow(FengShuiElement destiny, FengShuiElement element)
    {
        var product = ElementVector.Single(element).Scale(0.4m)
            .Add(ElementVector.Single(FengShuiElement.Tho).Scale(0.6m))
            .Normalize();

        var scored = Run(new MinorClashCase
        {
            Id = "SCORE-MC-06", Destiny = destiny, Product = product,
            Placement = ProductPlacement.Carry, MinorClashPenalty = 0m,
        });

        Assert.True(
            scored is null || scored.Breakdown!.Penalties
                .All(p => p.Code != ScoringParamCodes.MinorClashPenalty || !p.Applied),
            $"Mệnh {destiny} × hành {element}: kill-switch tắt mà vẫn đẻ ra dòng phạt.");
    }

    public static TheoryData<FengShuiElement, FengShuiElement> DestinyElementPairs()
    {
        var data = new TheoryData<FengShuiElement, FengShuiElement>();
        foreach (var destiny in Enum.GetValues<FengShuiElement>())
        foreach (var element in Enum.GetValues<FengShuiElement>())
            data.Add(destiny, element);
        return data;
    }

    // ===================== C. Tham số phải khớp seed =====================

    /// <summary>
    /// Default trong code phải khớp <c>seed-data/scoring-params.json</c>: lệch nhau thì môi trường
    /// thiếu row sẽ chấm khác prod — đúng vết <c>PERSONAL_WEIGHT_*</c> đã vấp.
    /// </summary>
    [Fact(DisplayName = "SCORE-PARAM-04 [Normal] The minor-clash default matches the seeded value")]
    public void ScoringParameters_MinorClashDefault_MatchesSeed()
        => Assert.Equal(0.60m, ScoringParameters.Default.MinorClashPenalty);
}

/// <summary>Một ca của §18 — xem <see cref="MinorClashPenaltyTests"/>.</summary>
public sealed class MinorClashCase
{
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    public FengShuiElement Destiny { get; init; } = FengShuiElement.Kim;
    public ElementVector Product { get; init; }
    public ProductPlacement Placement { get; init; } = ProductPlacement.Carry;

    /// <summary>Chỉ nhánh phòng đọc tới; nhánh Carry vốn đã 100% cá nhân.</summary>
    public decimal Wp { get; init; }

    public decimal MinorClashPenalty { get; init; } = 0.60m;

    /// <summary>Điểm kỳ vọng; <c>null</c> = kỳ vọng BỊ LOẠI khỏi danh sách.</summary>
    public decimal? Expected { get; init; }

    /// <summary>Giá trị dòng phạt phần phụ; <c>null</c> = kỳ vọng KHÔNG có dòng đó.</summary>
    public decimal? ExpectedMinorPenalty { get; init; }

    /// <summary>Mảnh chữ bắt buộc có trong <c>reasonVi</c> — khoá phần "ghi rõ" của waterfall.</summary>
    public string[] ReasonContains { get; init; } = Array.Empty<string>();

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
