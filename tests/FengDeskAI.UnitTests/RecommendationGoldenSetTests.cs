using System.Globalization;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// P2 — <b>Golden set</b> của engine gợi ý: 20 bộ (phòng × bản mệnh) với đáp án kỳ vọng cố định, mỗi
/// bộ chấm HAI lần — một lần ở <c>Wp = 0</c> (baseline, trục cá nhân TẮT) và một lần ở <c>Wp</c> đích
/// của <see cref="WorkspaceScope"/>. Xem <c>docs/adr/score-explainability-v3.2.md</c> P2.
///
/// <para><b>Vì sao cần, khác gì <see cref="RecommendationScorerTests"/>:</b> bộ kia chấm MỘT sản phẩm
/// trên bộ số nhân tạo để khoá từng luật rời. Bộ này chấm CẢ DANH SÁCH trên <b>vector phòng thật</b>
/// và so <b>thứ hạng</b> trước/sau khi bật trục cá nhân — thứ mà P2 phải trả lời: *"bật `Wp` lên thì
/// ranking đổi ra sao, và đổi có hợp lý không"*.</para>
///
/// <para><b>Đáp án kỳ vọng đến từ đâu:</b> tính bằng một bản cài đặt ĐỘC LẬP của công thức trong ADR
/// §8/§14, không lấy từ code C#. Lệch nhau ⇒ một trong hai sai, và ca đỏ chỉ đúng một dòng chuỗi nên
/// đọc được ngay sai ở sản phẩm nào.</para>
///
/// <para><b>Dữ liệu phòng</b> là snapshot của <c>seed-data/workspace-type-elements.json</c>:
/// <c>Ideal</c> = cột <c>ideal</c>, <c>Current</c> = cột <c>interior</c> — tương ứng phòng vừa tạo,
/// user chưa khai tag nào, chưa đặt sản phẩm nào (engine dựng <c>current</c> = normalize(interior×3
/// phiếu) = interior vì interior đã Σ=1). Sửa file seed đó thì bộ này ĐỎ — đúng ý đồ: đổi vector phòng
/// là đổi ranking của toàn hệ thống, phải nhìn thấy.</para>
///
/// <para><b>Cố định để cô lập đúng một biến:</b> <see cref="ProductPlacement.Living"/> (tắt Directional
/// Validation) + <see cref="WorkPurpose.Other"/> (tắt lọc vibe) ⇒ điểm chỉ còn phần ngũ hành. Hướng và
/// vibe đã có bộ ca riêng ở nhóm A4/A5.</para>
///
/// <para><b>Thứ tự khi hoà điểm:</b> danh mục truyền vào theo đúng thứ tự enum
/// <see cref="FengShuiElement"/> và <c>OrderByDescending</c> của LINQ là sort ổn định, nên hai sản phẩm
/// bằng điểm giữ nguyên thứ tự enum. Ngoài test, thứ tự giữa các sản phẩm hoà điểm không phải hợp đồng.</para>
/// </summary>
public sealed class RecommendationGoldenSetTests
{
    /// <summary>Danh mục ứng viên: 5 hành thuần — cho phép đọc thẳng đóng góp của từng hành ra ranking.</summary>
    private static readonly FengShuiElement[] Catalog = Enum.GetValues<FengShuiElement>();

    private static ScoringContext ContextOf(GoldenCase c, decimal wp) => new()
    {
        AdjustedIdeal = c.Ideal,
        CurrentVector = c.Current,
        PersonalVector = ElementVector.Single(c.Destiny),
        Scope = c.Scope,
        Purpose = WorkPurpose.Other,
        PersonalWeight = wp,
        Params = ScoringParameters.Default,
    };

    /// <summary>Chấm cả danh mục rồi rút gọn thành một dòng <c>"Hành:điểm"</c> theo thứ hạng.</summary>
    private static string RankOf(GoldenCase c, decimal wp)
    {
        var ids = Catalog.ToDictionary(e => Guid.NewGuid(), e => e);
        var candidates = ids
            .Select(kv => new ProductFacts(kv.Key, ElementVector.Single(kv.Value),
                new HashSet<string>(), ProductPlacement.Living))
            .ToList();

        var scored = new RecommendationScorer().Score(ContextOf(c, wp), candidates);
        // InvariantCulture: máy chạy locale vi-VN sẽ in dấu phẩy thập phân và ca đỏ oan.
        return string.Join(" ", scored.Select(
            s => $"{ids[s.ProductId]}:{s.Score.ToString("0.000", CultureInfo.InvariantCulture)}"));
    }

    [Theory(DisplayName = "GOLDEN")]
    [MemberData(nameof(Cases))]
    public void Ranking_AtBaselineAndTargetWeight_MatchesGoldenAnswer(GoldenCase c)
    {
        Assert.Equal(c.Baseline, RankOf(c, 0m));
        Assert.Equal(c.Live, RankOf(c, c.Wp));
    }

    /// <summary>
    /// Lý do P2 tồn tại: bật <c>PERSONAL_WEIGHT_*</c> phải THỰC SỰ đổi thứ hạng, không phải chỉ xê dịch
    /// vài con số. Ca này đỏ khi trục cá nhân bị vô hiệu hoá ở đâu đó trong đường đi.
    /// </summary>
    [Fact(DisplayName = "GOLDEN-01 [Normal] Turning the personal axis on reshuffles most rooms")]
    public void EnablingPersonalWeight_AcrossTheGoldenSet_ReordersMostRooms()
    {
        var reordered = All
            .Where(c => Elements(RankOf(c, 0m)) != Elements(RankOf(c, c.Wp)))
            .ToList();

        // 4 bộ KHÔNG đổi: 2 bộ Reception / Lounge (Public ⇒ Wp cố định 0, baseline ≡ live) và 2 bộ mà
        // hành phòng thiếu nhất tình cờ trùng hành bản mệnh ưu ái nhất.
        Assert.Equal(16, reordered.Count);
    }

    /// <summary>
    /// Hợp đồng của §8: trần toán học ±0.5 đã biến mất. Trước v3.2, <c>gapScore = gap·p / |gap|₁</c>
    /// không bao giờ vượt 0.5 ⇒ tier "Rất hợp" (<c>≥ 0.6</c>) là BẤT KHẢ THI, dù phòng lệch tới đâu.
    /// Ca này khẳng định điều đó đã hết, và hết ngay ở <b>baseline</b> — tức là do chuẩn hoá mẫu số,
    /// không phải nhờ cộng thêm điểm bản mệnh.
    /// </summary>
    [Fact(DisplayName = "GOLDEN-02 [Boundary] The top tier is reachable on real room data")]
    public void TopScore_OnRealRoomVectors_ClearsTheOldHalfScaleCeiling()
    {
        decimal topBaseline = All.Max(c => Top(RankOf(c, 0m)));
        decimal topLive = All.Max(c => Top(RankOf(c, c.Wp)));

        Assert.Equal(0.933m, topBaseline);
        Assert.Equal(0.717m, topLive);

        // Ngưỡng tier của FE (ScoreBadge.tsx): ≥0.6 "Rất hợp" · ≥0.2 "Phù hợp" · ≥−0.2 "Trung tính".
        Assert.True(topBaseline > 0.5m, $"Trần ±0.5 của v3.1 vẫn còn: đỉnh baseline chỉ {topBaseline}.");
        Assert.True(topLive >= 0.6m, $"Tier \"Rất hợp\" vẫn không với tới: đỉnh chỉ {topLive}.");
    }

    /// <summary>
    /// Hai người khác bản mệnh, cùng một phòng, cùng một danh mục ⇒ thứ hạng phải khác — TRỪ không gian
    /// <see cref="WorkspaceScope.Public"/>, nơi <c>Wp</c> cố định 0 và <c>PersonalConflictMode.None</c>
    /// (§14.3 · Q12): chỗ dùng chung không được neo vào bản mệnh của một người.
    /// </summary>
    [Fact(DisplayName = "GOLDEN-03 [Normal] Two destinies split the ranking everywhere except a public space")]
    public void SameRoomTwoDestinies_RankDifferently_UnlessTheSpaceIsPublic()
    {
        var pairs = All
            .GroupBy(c => c.Room)
            .Where(g => g.Count() == 2)
            .ToList();

        Assert.NotEmpty(pairs);
        foreach (var pair in pairs)
        {
            var (a, b) = (pair.First(), pair.Last());
            string rankA = Elements(RankOf(a, a.Wp)), rankB = Elements(RankOf(b, b.Wp));

            if (a.Scope == WorkspaceScope.Public)
                Assert.Equal(rankA, rankB);
            else
                Assert.True(rankA != rankB,
                    $"{a.Room}: mệnh {a.Destiny} và {b.Destiny} ra cùng thứ hạng {rankA} — trục cá nhân không có tác dụng.");
        }
    }

    private static string Elements(string ranking)
        => string.Join(" ", ranking.Split(' ').Select(x => x.Split(':')[0]));

    private static decimal Top(string ranking)
        => decimal.Parse(ranking.Split(' ')[0].Split(':')[1], CultureInfo.InvariantCulture);

    /// <summary>Bộ golden. Sinh bằng bản tham chiếu độc lập của công thức ADR §8/§14.</summary>
    private static readonly GoldenCase[] All =
    {
        new GoldenCase
        {
            Room = "Personal Desk", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Moc,
            Ideal = new(Tho: 0.25m, Kim: 0.2m, Thuy: 0.15m, Moc: 0.3m, Hoa: 0.1m),
            Current = new(Tho: 0.13m, Kim: 0.13m, Thuy: 0.07m, Moc: 0.6m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Tho:0.400 Thuy:0.267 Hoa:0.100 Moc:-1.000",
            Live = "Thuy:0.533 Tho:0.300 Moc:0.000 Hoa:-0.050 Kim:-0.683",
        },
        new GoldenCase
        {
            Room = "Personal Desk", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Kim,
            Ideal = new(Tho: 0.25m, Kim: 0.2m, Thuy: 0.15m, Moc: 0.3m, Hoa: 0.1m),
            Current = new(Tho: 0.13m, Kim: 0.13m, Thuy: 0.07m, Moc: 0.6m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Tho:0.400 Thuy:0.267 Kim:0.233 Moc:-1.000",
            Live = "Kim:0.617 Tho:0.600 Thuy:0.033 Moc:-0.400 Hoa:-0.750",
        },
        new GoldenCase
        {
            Room = "Home Office", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Thuy,
            Ideal = new(Tho: 0.2m, Kim: 0.2m, Thuy: 0.2m, Moc: 0.3m, Hoa: 0.1m),
            Current = new(Tho: 0.08m, Kim: 0.18m, Thuy: 0.07m, Moc: 0.6m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Thuy:0.433 Hoa:0.100 Kim:0.067 Moc:-1.000",
            Live = "Thuy:0.717 Kim:0.433 Hoa:0.150 Moc:-0.600 Tho:-0.600",
        },
        new GoldenCase
        {
            Room = "Private Office", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Moc,
            Ideal = new(Tho: 0.25m, Kim: 0.3m, Thuy: 0.15m, Moc: 0.2m, Hoa: 0.1m),
            Current = new(Tho: 0.17m, Kim: 0.6m, Thuy: 0.07m, Moc: 0.09m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Moc:0.367 Thuy:0.267 Tho:0.267 Hoa:0.100",
            Live = "Moc:0.683 Thuy:0.533 Tho:0.233 Hoa:-0.050 Kim:-1.000",
        },
        new GoldenCase
        {
            Room = "Bedroom", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Hoa,
            Ideal = new(Tho: 0.25m, Kim: 0.1m, Thuy: 0.3m, Moc: 0.25m, Hoa: 0.1m),
            Current = new(Tho: 0.25m, Kim: 0.07m, Thuy: 0.07m, Moc: 0.54m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Kim:0.103 Hoa:0.103 Tho:0.000 Moc:-1.000",
            Live = "Hoa:0.552 Kim:0.152 Moc:-0.100 Tho:-0.100 Thuy:-0.403",
        },
        new GoldenCase
        {
            Room = "Study Room", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Kim,
            Ideal = new(Tho: 0.2m, Kim: 0.15m, Thuy: 0.3m, Moc: 0.3m, Hoa: 0.05m),
            Current = new(Tho: 0.16m, Kim: 0.1m, Thuy: 0.09m, Moc: 0.57m, Hoa: 0.08m),
            Wp = 0.50m,
            Baseline = "Thuy:0.700 Kim:0.167 Tho:0.133 Moc:-0.900",
            Live = "Kim:0.583 Tho:0.467 Thuy:0.250 Moc:-0.350 Hoa:-0.850",
        },
        new GoldenCase
        {
            Room = "Bathroom", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Hoa,
            Ideal = new(Tho: 0.15m, Kim: 0.2m, Thuy: 0.45m, Moc: 0.1m, Hoa: 0.1m),
            Current = new(Tho: 0.06m, Kim: 0.06m, Thuy: 0.76m, Moc: 0.06m, Hoa: 0.06m),
            Wp = 0.50m,
            Baseline = "Kim:0.452 Tho:0.290 Moc:0.129 Hoa:0.129",
            Live = "Hoa:0.565 Moc:0.465 Kim:0.326 Tho:0.045 Thuy:-1.000",
        },
        new GoldenCase
        {
            Room = "Rooftop Garden", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Tho,
            Ideal = new(Tho: 0.15m, Kim: 0.05m, Thuy: 0.2m, Moc: 0.45m, Hoa: 0.15m),
            Current = new(Tho: 0.07m, Kim: 0.07m, Thuy: 0.07m, Moc: 0.72m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Thuy:0.448 Hoa:0.276 Tho:0.276 Kim:-0.069",
            Live = "Tho:0.638 Hoa:0.538 Thuy:0.324 Kim:-0.134 Moc:-1.000",
        },
        new GoldenCase
        {
            Room = "Meditation Room", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Kim,
            Ideal = new(Tho: 0.25m, Kim: 0.05m, Thuy: 0.35m, Moc: 0.3m, Hoa: 0.05m),
            Current = new(Tho: 0.23m, Kim: 0.07m, Thuy: 0.07m, Moc: 0.56m, Hoa: 0.07m),
            Wp = 0.50m,
            Baseline = "Thuy:0.933 Tho:0.067 Kim:-0.067 Moc:-0.867",
            Live = "Kim:0.467 Tho:0.433 Thuy:0.367 Moc:-0.333 Hoa:-0.833",
        },
        new GoldenCase
        {
            Room = "Garage", Scope = WorkspaceScope.Private, Destiny = FengShuiElement.Moc,
            Ideal = new(Tho: 0.25m, Kim: 0.45m, Thuy: 0.1m, Moc: 0.05m, Hoa: 0.15m),
            Current = new(Tho: 0.07m, Kim: 0.75m, Thuy: 0.06m, Moc: 0.06m, Hoa: 0.06m),
            Wp = 0.50m,
            Baseline = "Tho:0.581 Hoa:0.290 Thuy:0.129 Moc:-0.032",
            Live = "Moc:0.484 Thuy:0.465 Tho:0.390 Hoa:0.045 Kim:-1.000",
        },
        new GoldenCase
        {
            Room = "Kitchen", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Kim,
            Ideal = new(Tho: 0.25m, Kim: 0.15m, Thuy: 0.1m, Moc: 0.15m, Hoa: 0.35m),
            Current = new(Tho: 0.07m, Kim: 0.14m, Thuy: 0.07m, Moc: 0.07m, Hoa: 0.65m),
            Wp = 0.30m,
            Baseline = "Tho:0.600 Moc:0.267 Thuy:0.100 Kim:0.033 Hoa:-1.000",
            Live = "Tho:0.660 Kim:0.323 Moc:0.247 Thuy:0.010 Hoa:-1.000",
        },
        new GoldenCase
        {
            Room = "Kitchen", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Thuy,
            Ideal = new(Tho: 0.25m, Kim: 0.15m, Thuy: 0.1m, Moc: 0.15m, Hoa: 0.35m),
            Current = new(Tho: 0.07m, Kim: 0.14m, Thuy: 0.07m, Moc: 0.07m, Hoa: 0.65m),
            Wp = 0.30m,
            Baseline = "Moc:0.267 Thuy:0.100 Kim:0.033 Tho:0.000 Hoa:-1.000",
            Live = "Thuy:0.370 Kim:0.263 Moc:0.127 Tho:-0.060 Hoa:-0.640",
        },
        new GoldenCase
        {
            Room = "Living Room", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Moc,
            Ideal = new(Tho: 0.25m, Kim: 0.15m, Thuy: 0.15m, Moc: 0.25m, Hoa: 0.2m),
            Current = new(Tho: 0.52m, Kim: 0.09m, Thuy: 0.07m, Moc: 0.09m, Hoa: 0.23m),
            Wp = 0.30m,
            Baseline = "Moc:0.533 Thuy:0.267 Hoa:-0.100 Kim:-0.400 Tho:-0.900",
            Live = "Moc:0.673 Thuy:0.427 Hoa:-0.130 Kim:-0.340 Tho:-0.570",
        },
        new GoldenCase
        {
            Room = "Dining Room", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Thuy,
            Ideal = new(Tho: 0.35m, Kim: 0.15m, Thuy: 0.1m, Moc: 0.15m, Hoa: 0.25m),
            Current = new(Tho: 0.24m, Kim: 0.09m, Thuy: 0.08m, Moc: 0.44m, Hoa: 0.15m),
            Wp = 0.30m,
            Baseline = "Hoa:0.345 Kim:0.207 Thuy:0.069 Tho:-0.221 Moc:-1.000",
            Live = "Kim:0.385 Thuy:0.348 Hoa:0.301 Tho:-0.214 Moc:-0.760",
        },
        new GoldenCase
        {
            Room = "Meeting Room", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Moc,
            Ideal = new(Tho: 0.3m, Kim: 0.3m, Thuy: 0.1m, Moc: 0.2m, Hoa: 0.1m),
            Current = new(Tho: 0.2m, Kim: 0.59m, Thuy: 0.07m, Moc: 0.07m, Hoa: 0.07m),
            Wp = 0.30m,
            Baseline = "Moc:0.448 Tho:0.345 Thuy:0.103 Hoa:0.103 Kim:-1.000",
            Live = "Moc:0.614 Thuy:0.312 Tho:0.301 Hoa:0.012 Kim:-1.000",
        },
        new GoldenCase
        {
            Room = "Home Theater", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Kim,
            Ideal = new(Tho: 0.15m, Kim: 0.3m, Thuy: 0.1m, Moc: 0.15m, Hoa: 0.3m),
            Current = new(Tho: 0.07m, Kim: 0.19m, Thuy: 0.07m, Moc: 0.07m, Hoa: 0.6m),
            Wp = 0.30m,
            Baseline = "Kim:0.367 Moc:0.267 Tho:0.267 Thuy:0.100 Hoa:-1.000",
            Live = "Kim:0.557 Tho:0.427 Moc:0.247 Thuy:0.010 Hoa:-1.000",
        },
        new GoldenCase
        {
            Room = "Guest Room", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Hoa,
            Ideal = new(Tho: 0.3m, Kim: 0.2m, Thuy: 0.2m, Moc: 0.2m, Hoa: 0.1m),
            Current = new(Tho: 0.3m, Kim: 0.07m, Thuy: 0.07m, Moc: 0.49m, Hoa: 0.07m),
            Wp = 0.30m,
            Baseline = "Kim:0.448 Hoa:0.103 Tho:0.000 Thuy:-0.152 Moc:-1.000",
            Live = "Kim:0.374 Hoa:0.372 Tho:-0.060 Thuy:-0.166 Moc:-0.460",
        },
        new GoldenCase
        {
            Room = "Altar Room", Scope = WorkspaceScope.Shared, Destiny = FengShuiElement.Thuy,
            Ideal = new(Tho: 0.4m, Kim: 0.15m, Thuy: 0.05m, Moc: 0.1m, Hoa: 0.3m),
            Current = new(Tho: 0.17m, Kim: 0.08m, Thuy: 0.07m, Moc: 0.23m, Hoa: 0.45m),
            Wp = 0.30m,
            Baseline = "Kim:0.233 Tho:0.167 Thuy:-0.067 Moc:-0.433 Hoa:-0.500",
            Live = "Kim:0.403 Thuy:0.253 Tho:0.057 Hoa:-0.290 Moc:-0.363",
        },
        new GoldenCase
        {
            Room = "Reception / Lounge", Scope = WorkspaceScope.Public, Destiny = FengShuiElement.Moc,
            Ideal = new(Tho: 0.3m, Kim: 0.25m, Thuy: 0.1m, Moc: 0.15m, Hoa: 0.2m),
            Current = new(Tho: 0.6m, Kim: 0.13m, Thuy: 0.07m, Moc: 0.07m, Hoa: 0.13m),
            Wp = 0.00m,
            Baseline = "Kim:0.400 Moc:0.267 Hoa:0.233 Thuy:0.100 Tho:-1.000",
            Live = "Kim:0.400 Moc:0.267 Hoa:0.233 Thuy:0.100 Tho:-1.000",
        },
        new GoldenCase
        {
            Room = "Reception / Lounge", Scope = WorkspaceScope.Public, Destiny = FengShuiElement.Kim,
            Ideal = new(Tho: 0.3m, Kim: 0.25m, Thuy: 0.1m, Moc: 0.15m, Hoa: 0.2m),
            Current = new(Tho: 0.6m, Kim: 0.13m, Thuy: 0.07m, Moc: 0.07m, Hoa: 0.13m),
            Wp = 0.00m,
            Baseline = "Kim:0.400 Moc:0.267 Hoa:0.233 Thuy:0.100 Tho:-1.000",
            Live = "Kim:0.400 Moc:0.267 Hoa:0.233 Thuy:0.100 Tho:-1.000",
        },
    };

    public static TheoryData<GoldenCase> Cases()
    {
        var data = new TheoryData<GoldenCase>();
        foreach (var c in All) data.Add(c);
        return data;
    }
}

/// <summary>Một bộ golden: một phòng thật × một bản mệnh, kèm thứ hạng kỳ vọng ở hai mức <c>Wp</c>.</summary>
public sealed class GoldenCase
{
    /// <summary>Tên loại phòng trong <c>seed-data/workspace-type-elements.json</c>.</summary>
    public string Room { get; init; } = "";

    public WorkspaceScope Scope { get; init; }
    public FengShuiElement Destiny { get; init; }

    /// <summary>Cột <c>ideal</c> của loại phòng (chưa bẻ theo <c>WorkPurpose</c>).</summary>
    public ElementVector Ideal { get; init; }

    /// <summary>Cột <c>interior</c> — hiện trạng của phòng chưa khai gì thêm.</summary>
    public ElementVector Current { get; init; }

    /// <summary>Trọng số cá nhân đích của <see cref="Scope"/>: 0.50 / 0.30 / 0.00.</summary>
    public decimal Wp { get; init; }

    /// <summary>Thứ hạng khi trục cá nhân TẮT (<c>Wp = 0</c>) — hành vi v3 trên thang điểm mới.</summary>
    public string Baseline { get; init; } = "";

    /// <summary>Thứ hạng khi trục cá nhân BẬT ở <see cref="Wp"/>.</summary>
    public string Live { get; init; } = "";

    public override string ToString() => $"{Room} · {Scope} · mệnh {Destiny} · Wp={Wp:0.00}";
}
