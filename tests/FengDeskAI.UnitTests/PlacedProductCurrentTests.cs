using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// §19 — sản phẩm ĐÃ GIAO là hiện trạng thật của phòng, nên nó phải vào <c>current</c> dùng để CHẤM
/// ĐIỂM, không chỉ vào radar. Data-Driven: mỗi ca là một <see cref="PlacedCurrentCase"/>.
///
/// <para>
/// Lỗi đang bịt, dựng lại từ phòng thật "Phòng họp tổng" (Meeting Room · Shared · Office · chủ nhân
/// mệnh Kim · 0 tag · 2 sản phẩm đã giao). Trước §19, radar trang Workspace tính 2 sản phẩm đó còn
/// <c>GenerateAsync</c>/<c>GetProductFitAsync</c> thì không:
/// </para>
/// <list type="bullet">
/// <item>radar kết luận phòng thiếu <b>Thổ</b>;</item>
/// <item>bộ gợi ý kết luận phòng thiếu <b>Mộc</b> — đúng hành của cái cây tùng thơm user vừa mua về
/// đặt vào phòng, nên nó cứ đẩy tiếp đồ Mộc.</item>
/// </list>
///
/// <para>
/// <c>WorkspaceElementAnalyzer</c> vốn được tạo ra để "engine chấm điểm và endpoint element-analysis
/// dùng chung một công thức, bảo đảm Gap giống hệt nhau" — nhưng hai bên truyền tham số khác nhau nên
/// vẫn lệch. Ca ở đây khoá chính chỗ đó.
/// </para>
/// </summary>
public sealed class PlacedProductCurrentTests
{
    private static readonly Guid MeetingRoomId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    /// <summary>Meeting Room trong DB dev — nền phòng nghiêng hẳn về Kim.</summary>
    private static readonly (FengShuiElement Element, decimal Ideal, decimal Interior)[] MeetingRoom =
    {
        (FengShuiElement.Kim, 0.30m, 0.59m),
        (FengShuiElement.Tho, 0.30m, 0.20m),
        (FengShuiElement.Moc, 0.20m, 0.07m),
        (FengShuiElement.Hoa, 0.10m, 0.07m),
        (FengShuiElement.Thuy, 0.10m, 0.07m),
    };

    /// <summary>Cây tùng thơm: chỉ khai màu xanh ⇒ thuần Mộc.</summary>
    private static readonly ElementVector Thuja =
        new(Tho: 0m, Kim: 0m, Thuy: 0m, Moc: 1m, Hoa: 0m);

    /// <summary>Đèn muối Hymalaya: 0.6·(Crystal+SaltRock) + 0.4·(tròn + cam).</summary>
    private static readonly ElementVector SaltLamp =
        new(Tho: 0.18m, Kim: 0.35m, Thuy: 0.15m, Moc: 0m, Hoa: 0.32m);

    private static PersonPresence KimOwner => new(
        "Bạn — mệnh Kim", 2m,
        new ElementVector(Tho: 0.3m, Kim: 0.6m, Thuy: 0.1m, Moc: 0m, Hoa: 0m));

    // ===================== Bộ chạy chung =====================

    private static List<WorkspaceTypeElement> TypeElements() => MeetingRoom
        .SelectMany(x => new[]
        {
            new WorkspaceTypeElement
            {
                WorkspaceTypeId = MeetingRoomId, Source = WorkspaceElementSources.Ideal,
                Element = x.Element, Weight = x.Ideal,
            },
            new WorkspaceTypeElement
            {
                WorkspaceTypeId = MeetingRoomId, Source = WorkspaceElementSources.Interior,
                Element = x.Element, Weight = x.Interior,
            },
        })
        .ToList();

    /// <summary>Mục đích Office: Thổ +0.05, Kim +0.05.</summary>
    private static List<WorkPurposeElementModifier> OfficeModifiers() => new()
    {
        new() { WorkPurpose = WorkPurpose.Office, Element = FengShuiElement.Tho, Delta = 0.05m },
        new() { WorkPurpose = WorkPurpose.Office, Element = FengShuiElement.Kim, Delta = 0.05m },
    };

    private static WorkspaceElementAnalysis Analyze(IReadOnlyCollection<ProductContribution>? placed)
        => WorkspaceElementAnalyzer.Analyze(
            TypeElements(),
            OfficeModifiers(),
            Array.Empty<WorkspaceProfileInput>(),          // phòng này chưa khai tag nào
            new ElementInputResolver(Array.Empty<ElementInputMap>()),
            KimOwner,
            interiorVotes: 3m,
            saturationAlpha: 0.60m,
            placedProducts: placed);

    private static ElementVector Rounded(ElementVector v) => new(
        Math.Round(v.Tho, 6), Math.Round(v.Kim, 6), Math.Round(v.Thuy, 6),
        Math.Round(v.Moc, 6), Math.Round(v.Hoa, 6));

    // ===================== A. `current` và hệ quả trên `gap` =====================

    [Theory(DisplayName = "SCORE-PP")]
    [MemberData(nameof(Cases))]
    public void PlacedProducts_ChangeCurrentAndTheDominantNeed(PlacedCurrentCase c)
    {
        var analysis = Analyze(c.Placed
            .Select((v, i) => new ProductContribution(Guid.NewGuid(), $"SP{i}", v, 1m))
            .ToList());

        Assert.Equal(Rounded(c.ExpectedCurrent), Rounded(analysis.Current));
        Assert.Equal(c.ExpectedDominantNeed, analysis.Gap.Dominant());

        // adjustedIdeal KHÔNG phụ thuộc sản phẩm — nó là mục tiêu của loại phòng, không phải hiện trạng.
        Assert.Equal(
            Rounded(new ElementVector(Tho: 0.318182m, Kim: 0.318182m, Thuy: 0.090909m, Moc: 0.181818m, Hoa: 0.090909m)),
            Rounded(analysis.AdjustedIdeal));
    }

    public static TheoryData<PlacedCurrentCase> Cases()
    {
        var data = new TheoryData<PlacedCurrentCase>();

        data.Add(new PlacedCurrentCase
        {
            Id = "SCORE-PP-01", Name = "[Boundary] With no placed product the room reads as short of Wood",
            Placed = Array.Empty<ElementVector>(),
            ExpectedCurrent = new ElementVector(
                Tho: 0.253147m, Kim: 0.436031m, Thuy: 0.132903m, Moc: 0.088960m, Hoa: 0.088960m),
            ExpectedDominantNeed = FengShuiElement.Moc,
            Why = "Chỉ nền phòng 3 phiếu + chủ nhân 2 phiếu. Mộc chỉ 8.9% mà mục tiêu 18.2% ⇒ thiếu Mộc "
                + "nhất. Đây ĐÚNG LÀ con số mà bộ gợi ý dùng trước §19 — và nó sai vì phòng đã có cây.",
        });

        data.Add(new PlacedCurrentCase
        {
            Id = "SCORE-PP-02", Name = "[Normal] Counting the delivered products flips the dominant need to Earth",
            Placed = new[] { Thuja, SaltLamp },
            ExpectedCurrent = new ElementVector(
                Tho: 0.209961m, Kim: 0.355545m, Thuy: 0.122215m, Moc: 0.194036m, Hoa: 0.118243m),
            ExpectedDominantNeed = FengShuiElement.Tho,
            Why = "Cây tùng thơm (thuần Mộc) + đèn muối, mỗi thứ 1 phiếu ⇒ Mộc 8.9% → 19.4%, vượt cả mục "
                + "tiêu 18.2%. Hành thiếu nhất chuyển sang Thổ. Đây là con số radar vẫn hiện, và từ §19 "
                + "thì bộ gợi ý cũng dùng đúng nó.",
        });

        data.Add(new PlacedCurrentCase
        {
            Id = "SCORE-PP-03", Name = "[Abnormal] A product with an empty vector cannot dilute the room",
            Placed = new[] { Thuja, ElementVector.Zero },
            ExpectedCurrent = new ElementVector(
                Tho: 0.217209m, Kim: 0.374131m, Thuy: 0.114035m, Moc: 0.218293m, Hoa: 0.076331m),
            ExpectedDominantNeed = FengShuiElement.Tho,
            Why = "Kết quả phải Y HỆT ca chỉ có cây tùng thơm: vector 0 bị chặn ở HAI lớp độc lập "
                + "(PlacedProductVectorBuilder bỏ từ đầu, và BuildCurrentBreakdown cũng bỏ). Để nó lọt vào "
                + "thì nó ngốn một suất phiếu, làm loãng mọi hành khác mà không nói gì về căn phòng.",
        });

        return data;
    }

    // ===================== B. Builder: đã giao vs đang giao =====================

    /// <summary>
    /// Chỉ hàng ĐÃ GIAO là hiện trạng thật; hàng đang giao chỉ vào vector "xem trước". Đây là ranh
    /// giới khiến §19 không biến "vừa bấm mua" thành "phòng đã có".
    /// </summary>
    [Theory(DisplayName = "SCORE-PP-04 [Boundary] Only delivered items count as the room's real state")]
    [InlineData(DeliveryStatus.Delivered, 1, 1)]
    [InlineData(DeliveryStatus.Pending, 0, 1)]
    public void Builder_SplitsDeliveredFromPreview(
        DeliveryStatus status, int expectedDelivered, int expectedPreview)
    {
        var productId = Guid.NewGuid();
        var placement = new WorkspaceProductPlacement
        {
            Id = Guid.NewGuid(),
            OrderItemId = Guid.NewGuid(),
            ProductId = productId,
            OrderItem = new OrderItem
            {
                ProductName = "Cây tùng thơm",
                Delivery = new Delivery { Status = status },
            },
            Product = new Product
            {
                Id = productId,
                IsVectorOverridden = true,
                ElementTho = 0m, ElementKim = 0m, ElementThuy = 0m, ElementMoc = 1m, ElementHoa = 0m,
            },
        };

        var rows = PlacedProductVectorBuilder.Build(
            new[] { placement },
            new Dictionary<Guid, IReadOnlyCollection<ProductElementInput>>(),
            new ElementInputResolver(Array.Empty<ElementInputMap>()),
            ScoringParameters.Default);

        Assert.Equal(expectedDelivered, rows.DeliveredContributions().Count);
        Assert.Equal(expectedPreview, rows.PreviewContributions().Count);
        Assert.Equal(status.ToString(), rows.Single().DeliveryStatus);
        Assert.Equal(1.0m, rows.Single().VoteWeight);   // không gắn DecorItem ⇒ 1 phiếu, ngang một tag
    }

    // ===================== C. Phiên bản công thức =====================

    /// <summary>
    /// §12/§17/§18/§19 đổi công thức đủ nhiều để điểm không so trực tiếp được với phiên cũ. Không bump
    /// nhãn thì phiên lưu trước và sau cùng mang "3.2", và không ai truy được vì sao cùng một sản phẩm
    /// × cùng một phòng lại ra hai điểm khác nhau.
    /// </summary>
    [Fact(DisplayName = "SCORE-PP-05 [Normal] The engine stamps a formula version newer than 3.2")]
    public void FormulaVersion_IsBumpedPastV32()
    {
        Assert.NotEqual(
            Domain.Entities.CustomerCare.ScoringFormulaVersions.V32,
            Domain.Entities.CustomerCare.ScoringFormulaVersions.Current);
        // §19 đóng dấu 3.3; các đợt sau (N3 = 3.4, …) chỉ được tiến, không được lùi về trước 3.3.
        Assert.True(
            string.CompareOrdinal(
                Domain.Entities.CustomerCare.ScoringFormulaVersions.Current,
                Domain.Entities.CustomerCare.ScoringFormulaVersions.V33) >= 0);
    }
}

/// <summary>Một ca của §19 — xem <see cref="PlacedProductCurrentTests"/>.</summary>
public sealed class PlacedCurrentCase
{
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    /// <summary>Vector các sản phẩm ĐÃ GIAO đang đặt trong phòng, mỗi thứ 1 phiếu.</summary>
    public ElementVector[] Placed { get; init; } = Array.Empty<ElementVector>();

    public ElementVector ExpectedCurrent { get; init; }

    /// <summary>Hành thiếu nhất — thứ quyết định bộ gợi ý đi tìm gì.</summary>
    public FengShuiElement ExpectedDominantNeed { get; init; }

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
