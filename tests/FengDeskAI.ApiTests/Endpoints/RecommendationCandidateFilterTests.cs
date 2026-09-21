using FengDeskAI.ApiTests.Infrastructure;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Vendor;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FengDeskAI.ApiTests.Endpoints;

/// <summary>
/// Đợt 7 — tầng LỌC ứng viên của engine gợi ý. Bổ đôi cho <c>RecommendationScorerTests</c>: ở đó là
/// phép TÍNH ĐIỂM (thuần, in-memory), ở đây là bộ LỌC nằm trong LINQ→SQL của
/// <see cref="IProductRepository.GetScorableCandidatesAsync"/> — unit test không chạm tới được.
///
/// <para>
/// Chỉ khẳng định <b>CÓ / KHÔNG CÓ sản phẩm trong danh sách ứng viên</b>, không khẳng định điểm số:
/// điểm phụ thuộc dữ liệu seed, seed đổi là test vỡ oan. Mỗi lần chạy tự dựng một catalog riêng
/// (tên có hậu tố ngẫu nhiên) rồi assert theo Id, nên không phụ thuộc dữ liệu demo có sẵn.
/// </para>
///
/// <para><b>Điều kiện chạy:</b> cần migration tạo bảng <c>product_aspirations</c>
/// (<c>dotnet ef migrations add PersonalizedRecommendationV31</c>). Chưa có bảng thì các ca
/// B-35/B-36 đỏ vì SQL, không phải vì logic sai.</para>
///
/// Ranh giới đang chốt (xem <c>docs/adr/personalized-recommendation-v3.1.md</c> §4):
/// <list type="bullet">
/// <item><c>placements</c> là trục KHÔNG GIAN — luồng workspace lấy {Desk, Living}, luồng mang theo
/// người lấy {Carry}; <c>null</c> = không lọc (đọc lại phiên cũ).</item>
/// <item><c>aspiration</c> là trục MỤC TIÊU, độc lập hoàn toàn với <c>placements</c>;
/// <c>null</c> = KHÔNG lọc mục tiêu (không có giá trị mặc định).</item>
/// </list>
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class RecommendationCandidateFilterTests : IAsyncLifetime
{
    // Khóa logic của từng sản phẩm dựng cho bộ test này.
    private const string DeskPlain = "desk-plain";
    private const string LivingPlain = "living-plain";
    private const string CarryPlain = "carry-plain";
    private const string Consumable = "consumable";
    private const string DeskInactive = "desk-inactive";
    private const string DeskNoElement = "desk-no-element";
    private const string DeskWealthApproved = "desk-wealth-approved";
    private const string DeskWealthPending = "desk-wealth-pending";

    private static readonly ProductPlacement[] WorkspacePlacements =
        { ProductPlacement.Desk, ProductPlacement.Living };

    private static readonly ProductPlacement[] CarryPlacements = { ProductPlacement.Carry };

    private readonly ApiTestFixture _fixture;
    private readonly Dictionary<string, Guid> _ids = new();
    private readonly string _runId = Guid.NewGuid().ToString("N")[..8];

    public RecommendationCandidateFilterTests(ApiTestFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync() => await SeedCatalogAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ===================== Bảng ca =====================

    [Theory(DisplayName = "FILTER")]
    [MemberData(nameof(FilterCases))]
    public async Task GetScorableCandidates_AppliesExpectedFilters(FilterCase c)
    {
        List<Product> candidates = new();
        await _fixture.WithScopeAsync(async sp =>
        {
            var products = sp.GetRequiredService<IProductRepository>();
            candidates = await products.GetScorableCandidatesAsync(c.Placements, c.Aspiration);
        });

        var returned = candidates.Select(p => p.Id).ToHashSet();

        foreach (var key in c.Expected)
            Assert.True(returned.Contains(_ids[key]), $"{c.Id}: thiếu '{key}' trong ứng viên. {c.Why}");

        foreach (var key in c.NotExpected)
            Assert.False(returned.Contains(_ids[key]), $"{c.Id}: '{key}' KHÔNG được lọt vào ứng viên. {c.Why}");
    }

    public static TheoryData<FilterCase> FilterCases()
    {
        var data = new TheoryData<FilterCase>();

        data.Add(new FilterCase
        {
            Id = "FILTER-B-30", Name = "[Abnormal] An inactive product is never a candidate",
            Placements = WorkspacePlacements,
            Expected = new[] { DeskPlain }, NotExpected = new[] { DeskInactive },
            Why = "Điều kiện p.IsActive trong GetScorableCandidatesAsync.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-31", Name = "[Abnormal] A product with no declared elements is never a candidate",
            Placements = WorkspacePlacements,
            Expected = new[] { DeskPlain }, NotExpected = new[] { DeskNoElement },
            Why = "Không có ProductElement ⇒ không dựng được vector ⇒ engine không chấm được.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-32", Name = "[Normal] The workspace flow excludes carry items",
            Placements = WorkspacePlacements,
            Expected = new[] { DeskPlain, LivingPlain }, NotExpected = new[] { CarryPlain },
            Why = "POST /recommendations chỉ lấy {Desk, Living}.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-33", Name = "[Normal] The personal flow excludes room items",
            Placements = CarryPlacements,
            Expected = new[] { CarryPlain }, NotExpected = new[] { DeskPlain, LivingPlain },
            Why = "POST /recommendations/personal chỉ lấy {Carry}.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-34", Name = "[Abnormal] Consumables are excluded from the workspace flow",
            Placements = WorkspacePlacements,
            Expected = new[] { DeskPlain }, NotExpected = new[] { Consumable },
            Why = "Hàng tiêu hao không nằm trong bộ placement nào của hai luồng gợi ý.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-35", Name = "[Abnormal] Consumables are excluded from the personal flow too",
            Placements = CarryPlacements,
            Expected = new[] { CarryPlain }, NotExpected = new[] { Consumable },
            Why = "Cùng lý do B-34, kiểm ở luồng còn lại.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-36", Name = "[Normal] An approved goal tag passes the aspiration filter",
            Placements = WorkspacePlacements, Aspiration = FengDeskAI.Domain.Enums.Catalog.Aspiration.Wealth,
            Expected = new[] { DeskWealthApproved }, NotExpected = new[] { DeskPlain },
            Why = "Lọc cứng: chỉ sản phẩm được duyệt thẻ Wealth mới còn lại.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-37", Name = "[Abnormal] A goal tag the admin has not approved does not pass",
            Placements = WorkspacePlacements, Aspiration = FengDeskAI.Domain.Enums.Catalog.Aspiration.Wealth,
            Expected = new[] { DeskWealthApproved }, NotExpected = new[] { DeskWealthPending },
            Why = "IsApproved = false — vendor đề xuất chưa đủ để lên danh sách gợi ý.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-38", Name = "[Boundary] Omitting the aspiration applies no goal filter at all",
            Placements = WorkspacePlacements, Aspiration = null,
            Expected = new[] { DeskPlain, DeskWealthApproved, DeskWealthPending },
            NotExpected = Array.Empty<string>(),
            Why = "aspiration = null KHÔNG có giá trị mặc định: sản phẩm chưa gắn thẻ nào vẫn là ứng viên. "
                + "Trục placement mới quyết định luồng nào lấy gì — hai trục độc lập.",
        });
        data.Add(new FilterCase
        {
            Id = "FILTER-B-39", Name = "[Boundary] Omitting the placements applies no placement filter",
            Placements = null,
            Expected = new[] { DeskPlain, LivingPlain, CarryPlain, Consumable },
            NotExpected = new[] { DeskInactive, DeskNoElement },
            Why = "placements = null dùng khi đọc lại phiên cũ — cần đủ tên sản phẩm mọi loại; "
                + "nhưng IsActive/Elements vẫn là điều kiện cứng.",
        });

        return data;
    }

    // ===================== Dựng catalog riêng cho bộ test =====================

    private async Task SeedCatalogAsync()
    {
        await _fixture.WithScopeAsync(async sp =>
        {
            var db = sp.GetRequiredService<AppDbContext>();
            var storeId = await db.Set<GardenStore>().OrderBy(s => s.Name).Select(s => s.Id).FirstAsync();

            var seeds = new (string Key, ProductPlacement Placement, bool Active, FengShuiElement? Element,
                Aspiration? Goal, bool GoalApproved)[]
            {
                (DeskPlain, ProductPlacement.Desk, true, FengShuiElement.Moc, null, false),
                (LivingPlain, ProductPlacement.Living, true, FengShuiElement.Thuy, null, false),
                (CarryPlain, ProductPlacement.Carry, true, FengShuiElement.Kim, null, false),
                (Consumable, ProductPlacement.Consumable, true, FengShuiElement.Hoa, null, false),
                (DeskInactive, ProductPlacement.Desk, false, FengShuiElement.Moc, null, false),
                (DeskNoElement, ProductPlacement.Desk, true, null, null, false),
                (DeskWealthApproved, ProductPlacement.Desk, true, FengShuiElement.Moc, Aspiration.Wealth, true),
                (DeskWealthPending, ProductPlacement.Desk, true, FengShuiElement.Moc, Aspiration.Wealth, false),
            };

            foreach (var seed in seeds)
            {
                var product = new Product
                {
                    GardenStoreId = storeId,
                    Name = $"[TC {_runId}] {seed.Key}",
                    Description = "Sản phẩm dựng bởi RecommendationCandidateFilterTests.",
                    IsActive = seed.Active,
                    Placement = seed.Placement,
                };

                if (seed.Element is { } element)
                    product.Elements.Add(new ProductElement { Element = element, IsPrimary = true });

                if (seed.Goal is { } goal)
                    product.Aspirations.Add(new ProductAspiration
                    {
                        Aspiration = goal,
                        IsApproved = seed.GoalApproved,
                        ApprovedAt = seed.GoalApproved ? DateTime.UtcNow : null,
                    });

                await db.Set<Product>().AddAsync(product);
                _ids[seed.Key] = product.Id;
            }

            await db.SaveChangesAsync();
        });
    }
}

/// <summary>Một ca lọc ứng viên: gọi repo với bộ tham số nào, sản phẩm nào phải/không được có mặt.</summary>
public sealed class FilterCase
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Trục KHÔNG GIAN. <c>null</c> = không lọc placement.</summary>
    public ProductPlacement[]? Placements { get; init; }

    /// <summary>Trục MỤC TIÊU. <c>null</c> = không lọc mục tiêu (không có mặc định).</summary>
    public Aspiration? Aspiration { get; init; }

    public string[] Expected { get; init; } = Array.Empty<string>();
    public string[] NotExpected { get; init; } = Array.Empty<string>();

    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
