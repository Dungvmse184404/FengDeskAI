using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// Sản phẩm ĐÃ GIAO đặt trong phòng phải nằm trong <c>current</c> mà <b>bộ chấm điểm</b> dùng, không
/// chỉ trong <c>current</c> mà radar trang Workspace vẽ. Data-Driven: mỗi ca là một
/// <see cref="PlacedProductCase"/>.
///
/// <para>
/// Lỗi đang bịt, dựng lại từ hồ sơ thật "Phòng họp tổng" (Meeting Room · Shared · mục đích Office ·
/// chủ nhân mệnh Kim · KHÔNG có tag nào · 2 sản phẩm đã giao):
/// </para>
/// <list type="bullet">
/// <item>radar trang Workspace: có tính 2 sản phẩm ⇒ phòng thiếu <b>Thổ</b> (ĝ = +1.00)</item>
/// <item><c>GenerateAsync</c> + trang Fit: KHÔNG tính ⇒ phòng thiếu <b>Mộc</b> (ĝ = +0.58)</item>
/// </list>
/// <para>
/// Cùng lúc, cùng phòng, hai kết luận trái nhau — vì user đã mua và đặt một cây tùng thơm thuần Mộc
/// mà bộ gợi ý không nhìn thấy, nên nó cứ tiếp tục đẩy đồ Mộc. Điểm một sản phẩm thuần Mộc lệch
/// <b>0.694</b> trên thang ±1 giữa hai đường.
/// </para>
///
/// <para>
/// <see cref="WorkspaceElementAnalyzer"/> sinh ra đúng để chặn chuyện này ("bảo đảm Gap giống hệt
/// nhau") nhưng hai bên truyền tham số khác nhau nên vẫn lệch — nên ca này khoá ở tầng vector, chỗ
/// thấp nhất mà cả hai đường đều đi qua.
/// </para>
/// </summary>
public sealed class PlacedProductParityTests
{
    private static readonly Guid MeetingRoomId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private const decimal Alpha = 0.60m;

    /// <summary>Meeting Room trong DB dev.</summary>
    private static readonly (FengShuiElement Element, decimal Weight, string Source)[] TypeElements =
    {
        (FengShuiElement.Kim, 0.30m, WorkspaceElementSources.Ideal),
        (FengShuiElement.Tho, 0.30m, WorkspaceElementSources.Ideal),
        (FengShuiElement.Moc, 0.20m, WorkspaceElementSources.Ideal),
        (FengShuiElement.Hoa, 0.10m, WorkspaceElementSources.Ideal),
        (FengShuiElement.Thuy, 0.10m, WorkspaceElementSources.Ideal),
        (FengShuiElement.Kim, 0.59m, WorkspaceElementSources.Interior),
        (FengShuiElement.Tho, 0.20m, WorkspaceElementSources.Interior),
        (FengShuiElement.Hoa, 0.07m, WorkspaceElementSources.Interior),
        (FengShuiElement.Moc, 0.07m, WorkspaceElementSources.Interior),
        (FengShuiElement.Thuy, 0.07m, WorkspaceElementSources.Interior),
    };

    /// <summary>Mục đích Office: Thổ +0.05 · Kim +0.05.</summary>
    private static readonly (FengShuiElement Element, decimal Delta)[] OfficeModifiers =
    {
        (FengShuiElement.Tho, 0.05m), (FengShuiElement.Kim, 0.05m),
    };

    /// <summary>Chủ nhân mệnh Kim, 2 phiếu (scope Shared).</summary>
    private static PersonPresence KimOwner => new(
        "Bạn — mệnh Kim", 2m,
        new ElementVector(Tho: 0.3m, Kim: 0.6m, Thuy: 0.1m, Moc: 0m, Hoa: 0m));

    /// <summary>Cây tùng thơm: chỉ khai màu xanh ⇒ thuần Mộc.</summary>
    private static readonly ProductContribution Conifer =
        new(Guid.NewGuid(), "Cây tùng thơm", new ElementVector(Tho: 0m, Kim: 0m, Thuy: 0m, Moc: 1m, Hoa: 0m), 1m);

    /// <summary>Đèn muối Hymalaya: 0.6×(Crystal+SaltRock) + 0.4×(tròn+cam).</summary>
    private static readonly ProductContribution SaltLamp =
        new(Guid.NewGuid(), "Đèn muối Hymalaya",
            new ElementVector(Tho: 0.18m, Kim: 0.35m, Thuy: 0.15m, Moc: 0m, Hoa: 0.32m), 1m);

    // ===================== Bộ chạy chung =====================

    private static WorkspaceElementAnalysis Analyze(IReadOnlyCollection<ProductContribution> placed)
        => WorkspaceElementAnalyzer.Analyze(
            TypeElements.Select(x => new WorkspaceTypeElement
            {
                WorkspaceTypeId = MeetingRoomId, Element = x.Element, Weight = x.Weight, Source = x.Source,
            }).ToList(),
            OfficeModifiers.Select(x => new WorkPurposeElementModifier
            {
                WorkPurpose = WorkPurpose.Office, Element = x.Element, Delta = x.Delta,
            }).ToList(),
            Array.Empty<WorkspaceProfileInput>(),          // hồ sơ thật không khai tag nào
            new ElementInputResolver(new List<ElementInputMap>()),
            KimOwner,
            interiorVotes: 3m,
            saturationAlpha: Alpha,
            placedProducts: placed);

    private static ElementVector Rounded(ElementVector v, int digits = 6) => new(
        Math.Round(v.Tho, digits), Math.Round(v.Kim, digits), Math.Round(v.Thuy, digits),
        Math.Round(v.Moc, digits), Math.Round(v.Hoa, digits));

    // ===================== A. Giá trị chính xác =====================

    [Theory(DisplayName = "SCORE-PP")]
    [MemberData(nameof(Cases))]
    public void PlacedProducts_ChangeTheCurrentVectorUsedForScoring(PlacedProductCase c)
    {
        var analysis = Analyze(c.Placed);

        Assert.Equal(Rounded(c.ExpectedCurrent), Rounded(analysis.Current));
        Assert.Equal(c.ExpectedDominantNeed, analysis.Gap.Dominant());
    }

    public static TheoryData<PlacedProductCase> Cases()
    {
        var data = new TheoryData<PlacedProductCase>();

        data.Add(new PlacedProductCase
        {
            Id = "SCORE-PP-01", Name = "[Boundary] An empty room is described by its priors alone",
            Placed = Array.Empty<ProductContribution>(),
            ExpectedCurrent = new ElementVector(
                Tho: 0.253147m, Kim: 0.436031m, Thuy: 0.132903m, Moc: 0.088960m, Hoa: 0.088960m),
            ExpectedDominantNeed = FengShuiElement.Moc,
            Why = "Chỉ nền phòng 3 phiếu + chủ nhân 2 phiếu. Đây là thứ bộ gợi ý ĐANG thấy trước khi sửa: "
                + "phòng thiếu Mộc nhất, nên nó đề xuất thêm đồ Mộc.",
        });

        data.Add(new PlacedProductCase
        {
            Id = "SCORE-PP-02", Name = "[Normal] Delivered products move what the room is short of",
            Placed = new[] { Conifer, SaltLamp },
            ExpectedCurrent = new ElementVector(
                Tho: 0.209961m, Kim: 0.355545m, Thuy: 0.122215m, Moc: 0.194036m, Hoa: 0.118243m),
            ExpectedDominantNeed = FengShuiElement.Tho,
            Why = "Cây tùng thơm thuần Mộc kéo Mộc 8.9% → 19.4%, gần chạm mức lý tưởng 18.2%. "
                + "Hành thiếu nhất chuyển từ Mộc sang Thổ — đúng thứ radar đã hiện suốt, còn bộ gợi ý thì chưa.",
        });

        return data;
    }

    // ===================== B. Bất biến =====================

    /// <summary>
    /// Sản phẩm đã đặt phải đổi được KẾT LUẬN, không chỉ xê dịch số lẻ. Nếu một ngày ai đó lại truyền
    /// danh sách rỗng vào một trong hai đường, ca này đỏ ngay thay vì để hai màn hình lặng lẽ nói
    /// hai chuyện khác nhau về cùng một căn phòng.
    /// </summary>
    [Fact(DisplayName = "SCORE-PP-03 [Normal] Ignoring placed products flips which element the room needs")]
    public void IgnoringPlacedProducts_ChangesTheRecommendationTarget()
    {
        var without = Analyze(Array.Empty<ProductContribution>());
        var with = Analyze(new[] { Conifer, SaltLamp });

        Assert.NotEqual(without.Gap.Dominant(), with.Gap.Dominant());

        // Và lệch tới mức đổi dấu điểm: sản phẩm thuần Mộc từ "rất hợp" thành "không hợp".
        decimal GapScore(WorkspaceElementAnalysis a, FengShuiElement e)
        {
            decimal denominator = a.Gap.L1() / 2m;
            return denominator == 0m ? 0m : a.Gap[e] / denominator;
        }

        Assert.True(GapScore(without, FengShuiElement.Moc) > 0.5m);
        Assert.True(GapScore(with, FengShuiElement.Moc) < 0m);
    }
}

/// <summary>Một ca của bài toán "sản phẩm đã đặt" — xem <see cref="PlacedProductParityTests"/>.</summary>
public sealed class PlacedProductCase
{
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    public IReadOnlyCollection<ProductContribution> Placed { get; init; } = Array.Empty<ProductContribution>();

    public ElementVector ExpectedCurrent { get; init; }

    /// <summary>Hành có gap dương lớn nhất — thứ bộ gợi ý sẽ đi bù.</summary>
    public FengShuiElement ExpectedDominantNeed { get; init; }

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
