using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;
using Xunit.Abstractions;

namespace FengDeskAI.UnitTests;

/// <summary>
/// P6.6 của ADR <c>occupation-product-fit-v1.md</c>: chạy bộ golden (20 phòng × 5 sản phẩm thuần) với
/// <c>OCCUPATION_WEIGHT = 0.20</c> cho từng hồ sơ nghề seed, đối chiếu với <c>Wo = 0</c>.
///
/// <para>
/// Bộ golden gốc không có nghề (mọi ca <c>Wo = 0</c>) nên vẫn byte-identical; file này là lớp phủ thêm:
/// hồ sơ nghề đọc thẳng từ <c>seed-data/occupation-element-profiles.json</c> để test và DB không thể
/// lệch nhau. Ba điều khẳng định, còn thứ hạng đổi thế nào thì in ra để người đọc soát (P6.6 là bước
/// "soát thứ hạng", không phải đóng đinh số).
/// </para>
/// </summary>
public sealed class OccupationGoldenSetTests
{
    private const decimal Wo = 0.20m;

    private static readonly FengShuiElement[] Catalog = Enum.GetValues<FengShuiElement>();

    private readonly ITestOutputHelper _output;

    public OccupationGoldenSetTests(ITestOutputHelper output) => _output = output;

    private static List<GoldenCase> Cases()
        => ((IEnumerable<GoldenCase>)RecommendationGoldenSetTests.Cases()).ToList();

    /// <summary>
    /// Gốc repo tìm theo hai đường: đi ngược từ thư mục chạy test (bin/ trong repo — CI, IDE), rồi từ đường
    /// dẫn file nguồn này (<see cref="CallerFilePathAttribute"/> — build ra thư mục ngoài repo bằng <c>-o</c>).
    /// </summary>
    private static string RepoRoot([CallerFilePath] string sourcePath = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(sourcePath) })
        {
            var dir = start is null ? null : new DirectoryInfo(start);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FengDeskAI.slnx")))
                dir = dir.Parent;
            if (dir is not null) return dir.FullName;
        }
        throw new InvalidOperationException("Không tìm thấy gốc repo (FengDeskAI.slnx).");
    }

    /// <summary>Hồ sơ Σ=1 theo nghề, đúng file seed đang chạy.</summary>
    private static IReadOnlyDictionary<string, ElementVector> SeededProfiles()
    {
        var path = Path.Combine(RepoRoot(), "seed-data", "occupation-element-profiles.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var rows = doc.RootElement.TryGetProperty("rows", out var r) ? r : doc.RootElement;

        return rows.EnumerateArray()
            .GroupBy(x => x.GetProperty("occupation").GetString()!)
            .ToDictionary(
                g => g.Key,
                g => OccupationProfileRules.ToVector(g
                    .Select(x => (Enum.Parse<FengShuiElement>(x.GetProperty("element").GetString()!),
                                  x.GetProperty("share").GetDecimal()))
                    .ToList()));
    }

    private static ScoringContext ContextOf(GoldenCase c, ElementVector? profile, string code, decimal wo)
    {
        var p = ScoringParameters.Default with { OccupationWeight = wo };
        return new ScoringContext
        {
            AdjustedIdeal = c.Ideal,
            CurrentVector = c.Current,
            PersonalVector = ElementVector.Single(c.Destiny),
            Scope = c.Scope,
            Purpose = WorkPurpose.Other,
            PersonalWeight = c.Wp,
            OccupationProfile = profile,
            OccupationWeight = p.OccupationWeightFor(c.Wp),
            OccupationCode = code,
            OccupationNameVi = code,
            Params = p,
        };
    }

    private static List<(FengShuiElement Element, decimal Score)> Rank(GoldenCase c, ElementVector? profile, string code, decimal wo)
    {
        var ids = Catalog.ToDictionary(e => Guid.NewGuid(), e => e);
        var candidates = ids
            .Select(kv => new ProductFacts(kv.Key, ElementVector.Single(kv.Value), new HashSet<string>(), ProductPlacement.Living))
            .ToList();
        return new RecommendationScorer().Score(ContextOf(c, profile, code, wo), candidates)
            .Select(s => (ids[s.ProductId], s.Score)).ToList();
    }

    private static string Line(IEnumerable<(FengShuiElement Element, decimal Score)> rank)
        => string.Join(" ", rank.Select(x => $"{x.Element}:{x.Score.ToString("0.000", CultureInfo.InvariantCulture)}"));

    [Fact(DisplayName = "OCC-GOLDEN-01 [Normal] Wo = 0.20 shifts every product by at most 2·Wo and only through ô·p")]
    public void OccupationWeight_OnGoldenSet_ShiftsAreBoundedAndExplained()
    {
        var profiles = SeededProfiles();
        var cases = Cases();
        Assert.NotEmpty(cases);

        int topChanged = 0, orderChanged = 0, total = 0;
        foreach (var (code, profile) in profiles.OrderBy(kv => kv.Key))
        {
            _output.WriteLine($"=== {code} — hồ sơ {Line(Catalog.Select(e => (e, profile[e])))}");
            foreach (var c in cases)
            {
                var before = Rank(c, profile, code, 0m);
                var after = Rank(c, profile, code, Wo);
                total++;

                // Wo·(ô·p − gapScore·…) — mỗi số hạng |·| ≤ 1 ⇒ |Δ| ≤ 2·Wo. Vượt là có gì đó ngoài trục nghề đổi.
                var byElement = before.ToDictionary(x => x.Element, x => x.Score);
                foreach (var (e, s) in after)
                    Assert.InRange(Math.Abs(s - byElement[e]), 0m, 2m * Wo + 0.001m);

                bool sameOrder = before.Select(x => x.Element).SequenceEqual(after.Select(x => x.Element));
                bool sameTop = before[0].Element == after[0].Element;
                if (!sameOrder) orderChanged++;
                if (!sameTop) topChanged++;

                if (!sameOrder)
                    _output.WriteLine($"  {c,-55} {(sameTop ? "  " : "▲ ")}{Line(before)}  →  {Line(after)}");
            }
        }

        _output.WriteLine($"--- {profiles.Count} nghề × {cases.Count} phòng = {total} lượt: đổi thứ tự {orderChanged}, đổi top-1 {topChanged}");
        Assert.True(orderChanged > 0, "Wo = 0.20 mà không đổi thứ hạng ở đâu cả — trục nghề không có tác dụng.");
    }

    [Fact(DisplayName = "OCC-GOLDEN-02 [Boundary] The OTHER profile (uniform) leaves the golden set byte-identical at Wo = 0.20")]
    public void OtherProfile_OnGoldenSet_IsIdentical()
    {
        var other = SeededProfiles()["OTHER"];
        foreach (var c in Cases())
            Assert.Equal(Line(Rank(c, other, "OTHER", 0m)), Line(Rank(c, other, "OTHER", Wo)));
    }

    [Fact(DisplayName = "OCC-GOLDEN-03 [Normal] With Wo = 0.20 the top-1 never becomes a product that clashes the destiny")]
    public void OccupationWeight_NeverPromotesAClashingProductToTop()
    {
        var profiles = SeededProfiles();
        foreach (var c in Cases())
        {
            if (c.Scope == WorkspaceScope.Public) continue; // Public: không mệnh ⇒ không có "khắc mệnh" để chặn
            foreach (var (code, profile) in profiles)
            {
                var top = Rank(c, profile, code, Wo)[0].Element;
                Assert.NotEqual(FengShuiRelation.BiKhac, FengShuiCalculator.GetRelation(c.Destiny, top));
            }
        }
    }
}
