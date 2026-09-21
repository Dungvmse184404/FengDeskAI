using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// v3.5 — trần TỔNG phiếu tag khi dựng <c>current</c> (ADR <c>current-tag-votes-cap-v3.5.md</c>).
///
/// <code>
/// tagVotes = Σ_tag votes_tag
/// k        = tagVotes &gt; cap ? cap / tagVotes : 1
/// m[e]     = 3·interior[e] + votes_chủNhân·pv[e] + k·Σ_tag votes_tag·v_tag[e] + Σ_sp voteWeight·p_sp[e]
/// </code>
///
/// <para>
/// Mỗi tag 1 phiếu trỏ trọn vào một hành, nền phòng đều 0.2 (loại phòng không seed Interior) — để kỳ
/// vọng tính tay được. <c>α = 1</c> ở các ca tính giá trị để tách riêng cap khỏi nén §17; ca bất biến chạy
/// cả <c>α = 0.6</c>.
/// </para>
/// </summary>
public sealed class TagVotesCapTests
{
    private const decimal Cap = 5m;
    private const decimal Seeded = 0.60m;

    private static ElementInputResolver Resolver() => new(
        Enum.GetValues<FengShuiElement>().Select(e => new ElementInputMap
        {
            InputKind = ElementInputKind.DecorItem,
            InputCode = e.ToString(),
            LabelVi = e.ToString(),
            Element = e,
            Weight = 1m,
        }).ToList());

    private static List<WorkspaceProfileInput> Tags(params (FengShuiElement Element, int Count)[] spec)
        => spec.SelectMany(s => Enumerable.Range(0, s.Count).Select(_ => new WorkspaceProfileInput
        {
            InputKind = ElementInputKind.DecorItem,
            InputCode = s.Element.ToString(),
        })).ToList();

    private static PersonPresence KimOwner(decimal votes = 3m) => new(
        "Bạn — mệnh Kim", votes,
        new ElementVector(Tho: 0.3m, Kim: 0.6m, Thuy: 0.1m, Moc: 0m, Hoa: 0m));

    private static CurrentBreakdown Build(
        List<WorkspaceProfileInput> tags,
        decimal? cap,
        decimal alpha = 1m,
        PersonPresence? person = null,
        IReadOnlyCollection<ProductContribution>? products = null)
        => WorkspaceVectorBuilder.BuildCurrentBreakdown(
            tags, Resolver(), Array.Empty<WorkspaceTypeElement>(),
            products ?? Array.Empty<ProductContribution>(),
            person ?? KimOwner(), interiorVotes: 3m, saturationAlpha: alpha, tagVotesCap: cap);

    private static ElementVector Rounded(ElementVector v, int digits = 6) => new(
        Math.Round(v.Tho, digits), Math.Round(v.Kim, digits), Math.Round(v.Thuy, digits),
        Math.Round(v.Moc, digits), Math.Round(v.Hoa, digits));

    private static IEnumerable<CurrentContribution> TagRows(CurrentBreakdown b)
        => b.Contributions.Where(c => c.Source == CurrentSourceKind.Tag);

    // ===================== A. Giá trị =====================

    [Theory(DisplayName = "CAP-01 [Boundary] At or below the cap the breakdown is byte-identical to no cap")]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(5)]
    public void Cap_TagVotesWithinCap_LeavesEverythingUntouched(int tagCount)
    {
        var tags = Tags((FengShuiElement.Moc, tagCount));

        foreach (decimal alpha in new[] { 1m, Seeded })
        {
            var capped = Build(tags, Cap, alpha);
            var free = Build(tags, null, alpha);

            Assert.Equal(free.Current, capped.Current);
            Assert.Equal(free.TotalVotes, capped.TotalVotes);
            Assert.Equal(1m, capped.TagVotesScale);
            Assert.Equal(TagRows(free).Select(c => c.Votes), TagRows(capped).Select(c => c.Votes));
        }
    }

    [Fact(DisplayName = "CAP-02 [Normal] Seven tags are scaled to 5/7 each, summing to exactly the cap")]
    public void Cap_SevenTags_EachScaledToFiveSevenths()
    {
        var tags = Tags((FengShuiElement.Moc, 4), (FengShuiElement.Hoa, 2), (FengShuiElement.Kim, 1));
        var b = Build(tags, Cap);

        decimal k = Cap / 7m;
        Assert.Equal(k, b.TagVotesScale);
        Assert.All(TagRows(b), c => Assert.Equal(k, c.Votes));
        Assert.Equal(Cap, Math.Round(TagRows(b).Sum(c => c.Votes), 9));

        // Nền 3 + chủ nhân 3 + tag 5 = 11 — tag không thể vượt nền + chủ nhân.
        Assert.Equal(11m, Math.Round(b.TotalVotes, 9));

        // α = 1: current = m / 11, tính tay.
        // m[Moc] = 3·0.2 + 4k = 0.6 + 20/7 ; m[Hoa] = 0.6 + 10/7 ; m[Kim] = 0.6 + 3·0.6 + 5/7
        // m[Tho] = 0.6 + 0.9 ; m[Thuy] = 0.6 + 0.3
        var expected = new ElementVector(
            Tho: 1.5m / 11m, Kim: (0.6m + 1.8m + 5m / 7m) / 11m, Thuy: 0.9m / 11m,
            Moc: (0.6m + 20m / 7m) / 11m, Hoa: (0.6m + 10m / 7m) / 11m);
        Assert.Equal(Rounded(expected), Rounded(b.Current));
    }

    [Fact(DisplayName = "CAP-03 [Boundary] The cap is continuous: 5 tags and 6 tags×(5/6) draw the same room")]
    public void Cap_IsContinuousAtTheCap()
    {
        // Sáu tag cùng hành, cap 5 ⇒ mỗi tag 5/6 ⇒ tổng khối lượng tag = 5 = đúng năm tag nguyên.
        var six = Build(Tags((FengShuiElement.Moc, 6)), Cap);
        var five = Build(Tags((FengShuiElement.Moc, 5)), Cap);

        Assert.Equal(Rounded(five.Current), Rounded(six.Current));
        Assert.Equal(five.TotalVotes, six.TotalVotes);
    }

    [Fact(DisplayName = "CAP-04 [Normal] A capped room still lets one placed product move the current")]
    public void Cap_TwentyTags_ProductRemainsVisible()
    {
        // 20 tag Mộc, sản phẩm 100% Thủy 1 phiếu. Không cap: sản phẩm là 1/27; có cap: 1/12.
        var tags = Tags((FengShuiElement.Moc, 20));
        var product = new[] { new ProductContribution(Guid.NewGuid(), "Bể cá", ElementVector.Single(FengShuiElement.Thuy), 1m) };

        decimal ShiftThuy(decimal? cap, decimal alpha)
            => Build(tags, cap, alpha, products: product).Current.Thuy - Build(tags, cap, alpha).Current.Thuy;

        // α = 1 (tuyến tính): 1.6/12 − 0.6/11 ≈ 0.079 so với 1.6/27 − 0.6/26 ≈ 0.036 — hơn gấp đôi.
        Assert.True(ShiftThuy(Cap, 1m) > 2m * ShiftThuy(null, 1m));

        // α = 0.6: nén §17 vốn đã nhấc hành yếu lên nên chênh lệch nhỏ hơn, nhưng cap vẫn phải làm rõ hơn.
        Assert.True(ShiftThuy(Cap, Seeded) > ShiftThuy(null, Seeded));

        Assert.Equal(12m, Math.Round(Build(tags, Cap, Seeded, products: product).TotalVotes, 9));
    }

    // ===================== B. Bất biến =====================

    [Fact(DisplayName = "CAP-05 [Normal] Evidence count and confidence count evidence, not votes")]
    public void Cap_DoesNotChangeEvidenceCount()
    {
        var tags = Tags((FengShuiElement.Moc, 7));
        var capped = Build(tags, Cap);
        var free = Build(tags, null);

        Assert.Equal(7, capped.EvidenceCount);
        Assert.Equal(free.EvidenceCount, capped.EvidenceCount);
    }

    [Theory(DisplayName = "CAP-06 [Normal] Per-source shares still add up to the current on every axis")]
    [InlineData(1.0)]
    [InlineData(0.6)]
    public void Cap_ShareOf_StillSumsToCurrent(decimal alpha)
    {
        var b = Build(Tags((FengShuiElement.Moc, 6), (FengShuiElement.Hoa, 3)), Cap, alpha);

        foreach (var e in Enum.GetValues<FengShuiElement>())
        {
            decimal sum = b.Contributions.Sum(c => b.ShareOf(c, e));
            Assert.Equal(Math.Round(b.Current[e], 9), Math.Round(sum, 9));
        }
    }

    [Fact(DisplayName = "CAP-07 [Normal] Interior, owner and products are never scaled — only tags are")]
    public void Cap_OnlyTagsAreScaled()
    {
        var product = new[] { new ProductContribution(Guid.NewGuid(), "Bể cá", ElementVector.Single(FengShuiElement.Thuy), 1.5m) };
        var b = Build(Tags((FengShuiElement.Moc, 10)), Cap, products: product);

        Assert.Equal(3m, b.Contributions.Single(c => c.Source == CurrentSourceKind.Interior).Votes);
        Assert.Equal(3m, b.Contributions.Single(c => c.Source == CurrentSourceKind.Person).Votes);
        Assert.Equal(1.5m, b.Contributions.Single(c => c.Source == CurrentSourceKind.Product).Votes);
        Assert.Equal(0.5m, b.TagVotesScale);
    }

    [Theory(DisplayName = "CAP-08 [Boundary] Cap ≤ 0 is the kill-switch")]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cap_NonPositive_IsOff(decimal cap)
    {
        var tags = Tags((FengShuiElement.Moc, 9));
        var off = Build(tags, cap);
        var free = Build(tags, null);

        Assert.Equal(free.Current, off.Current);
        Assert.Equal(1m, off.TagVotesScale);
        Assert.Equal(15m, off.TotalVotes);
    }

    // ===================== C. Tham số =====================

    [Fact(DisplayName = "CAP-09 [Normal] Defaults match the seeded values (cap 5, Wp 0.30/0.20) and stamp ≥ 3.5")]
    public void ScoringParameters_Defaults_MatchSeed()
    {
        var d = ScoringParameters.Default;
        Assert.Equal(5.00m, d.TagVotesCap);
        Assert.Equal(0.30m, d.PersonalWeightPrivate);
        Assert.Equal(0.20m, d.PersonalWeightShared);
        Assert.Equal(0.00m, d.PersonalWeightPublic);
        // Cap vào từ 3.5; bản sau vẫn phải mang nó ⇒ so ≥ thay vì đóng đinh.
        Assert.True(string.CompareOrdinal(ScoringFormulaVersions.Current, ScoringFormulaVersions.V35) >= 0);
    }

    [Fact(DisplayName = "CAP-10 [Normal] TAG_VOTES_CAP is read from the params table")]
    public void ScoringParameters_FromRows_ReadsCap()
    {
        var p = ScoringParameters.FromRows(new[]
        {
            new ScoringParam { Code = ScoringParamCodes.TagVotesCap, Value = 8m },
        });
        Assert.Equal(8m, p.TagVotesCap);
    }
}
