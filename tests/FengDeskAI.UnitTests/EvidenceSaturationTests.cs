using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// §17 — nén tương phản khi dựng <c>current</c>. Data-Driven: mỗi ca là một
/// <see cref="SaturationCase"/>, thêm ca = thêm một dòng dữ liệu.
///
/// <code>
/// m[e]    = Σᵢ vᵢ · wᵢ[e]          // khối lượng thô, đơn vị PHIẾU
/// current = normalize( m^α )       // α = EVIDENCE_SATURATION_ALPHA
/// </code>
///
/// <para>
/// Tật đang sửa: khai 12 tag Mộc thì Mộc chiếm ~56% hiện trạng và nuốt gần hết bốn hành còn lại, dù
/// 12 tag đó chỉ nói "phòng nhiều gỗ" chứ không nói "phòng gấp 12 lần gỗ".
/// </para>
///
/// <para>
/// Hai tầng khẳng định: <b>A</b> chốt GIÁ TRỊ CHÍNH XÁC của <c>current</c> (tính tay từ công thức
/// trên, không lấy ngược từ code); <b>B</b> chạy mọi BẤT BIẾN trên cùng bộ ca đó — nên thêm một dòng
/// dữ liệu là tự động được kiểm cả bảy tính chất, không phải viết thêm hàm nào.
/// </para>
/// </summary>
public sealed class EvidenceSaturationTests
{
    private static readonly Guid KitchenTypeId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private const decimal Seeded = 0.60m;

    /// <summary>Nhà bếp chính trong DB dev — nền phòng nghiêng hẳn về Hỏa.</summary>
    private static readonly (FengShuiElement Element, decimal Weight)[] KitchenInterior =
    {
        (FengShuiElement.Hoa, 0.65m), (FengShuiElement.Kim, 0.14m), (FengShuiElement.Moc, 0.07m),
        (FengShuiElement.Tho, 0.07m), (FengShuiElement.Thuy, 0.07m),
    };

    /// <summary>Chủ nhân mệnh Kim: 60% Kim · 30% Thổ (sinh Kim) · 10% Thủy (Kim sinh).</summary>
    private static PersonPresence KimOwner(decimal votes) => new(
        "Bạn — mệnh Kim", votes,
        new ElementVector(Tho: 0.3m, Kim: 0.6m, Thuy: 0.1m, Moc: 0m, Hoa: 0m));

    // ===================== Bộ chạy chung =====================

    private static List<WorkspaceTypeElement> Interior() => KitchenInterior
        .Select(x => new WorkspaceTypeElement
        {
            WorkspaceTypeId = KitchenTypeId,
            Source = WorkspaceElementSources.Interior,
            Element = x.Element,
            Weight = x.Weight,
        })
        .ToList();

    /// <summary>Mỗi tag đúng 1 phiếu và trỏ trọn vào một hành — để kỳ vọng tính tay được.</summary>
    private static ElementInputResolver Resolver() => new(
        Enum.GetValues<FengShuiElement>().Select(e => new ElementInputMap
        {
            InputKind = ElementInputKind.DecorItem,
            InputCode = e.ToString(),
            LabelVi = e.ToString(),
            Element = e,
            Weight = 1m,
        }).ToList());

    private static List<WorkspaceProfileInput> Tags(IEnumerable<(FengShuiElement Element, int Count)> spec)
        => spec.SelectMany(s => Enumerable.Range(0, s.Count).Select(_ => new WorkspaceProfileInput
        {
            InputKind = ElementInputKind.DecorItem,
            InputCode = s.Element.ToString(),
        })).ToList();

    private static CurrentBreakdown Build(SaturationCase c, decimal alpha, decimal scale = 1m)
        => WorkspaceVectorBuilder.BuildCurrentBreakdown(
            Tags(c.Tags.Select(t => (t.Element, Count: (int)(t.Count * scale)))),
            Resolver(),
            Interior(),
            Array.Empty<ProductContribution>(),
            KimOwner(c.PersonVotes * scale),
            interiorVotes: 3m * scale,
            saturationAlpha: alpha);

    private static ElementVector Rounded(ElementVector v, int digits = 6) => new(
        Math.Round(v.Tho, digits), Math.Round(v.Kim, digits), Math.Round(v.Thuy, digits),
        Math.Round(v.Moc, digits), Math.Round(v.Hoa, digits));

    // ===================== A. Giá trị chính xác của `current` =====================

    [Theory(DisplayName = "SCORE-SAT")]
    [MemberData(nameof(Cases))]
    public void Saturation_ProducesExpectedCurrentVector(SaturationCase c)
    {
        var actual = Build(c, c.Alpha).Current;
        Assert.Equal(Rounded(c.ExpectedCurrent), Rounded(actual));
    }

    public static TheoryData<SaturationCase> Cases()
    {
        var data = new TheoryData<SaturationCase>();

        var flooded = new[]
        {
            (FengShuiElement.Moc, 12), (FengShuiElement.Kim, 2),
            (FengShuiElement.Tho, 2), (FengShuiElement.Hoa, 1),
        };

        data.Add(new SaturationCase
        {
            Id = "SCORE-SAT-01", Name = "[Boundary] Alpha one is the plain linear vote model",
            Tags = flooded, Alpha = 1.00m,
            ExpectedCurrent = new ElementVector(
                Tho: 0.127727m, Kim: 0.164545m, Thuy: 0.018636m, Moc: 0.555000m, Hoa: 0.134091m),
            Why = "m^1 = m ⇒ current = khối lượng thô / 22 phiếu. Mộc 12+0.21 = 12.21 ⇒ 55.5%: "
                + "đúng tật đang sửa, một hành nuốt hơn nửa hiện trạng.",
        });

        data.Add(new SaturationCase
        {
            Id = "SCORE-SAT-02", Name = "[Normal] Saturation curbs the flooded element without erasing it",
            Tags = flooded, Alpha = Seeded,
            ExpectedCurrent = new ElementVector(
                Tho: 0.168827m, Kim: 0.196537m, Thuy: 0.053197m, Moc: 0.407613m, Hoa: 0.173825m),
            Why = "α=0.6 kéo Mộc 55.5% → 40.8% mà Mộc VẪN trội — phòng đó đúng là nhiều gỗ thật, "
                + "nén không được xoá mất sự thật đó. Thủy (yếu nhất) được nhấc 1.9% → 5.3%.",
        });

        data.Add(new SaturationCase
        {
            Id = "SCORE-SAT-03", Name = "[Normal] A balanced room is barely moved by saturation",
            Tags = new[] { (FengShuiElement.Moc, 2), (FengShuiElement.Tho, 2), (FengShuiElement.Kim, 2) },
            Alpha = Seeded,
            ExpectedCurrent = new ElementVector(
                Tho: 0.241070m, Kim: 0.280637m, Thuy: 0.075961m, Moc: 0.208716m, Hoa: 0.193616m),
            Why = "Không hành nào áp đảo thì nén gần như không có việc gì để làm — đúng ý đồ: "
                + "nó chỉ hãm phần tương phản thừa, không bóp phẳng mọi căn phòng.",
        });

        data.Add(new SaturationCase
        {
            Id = "SCORE-SAT-04", Name = "[Abnormal] A room with no tags still saturates cleanly",
            Tags = Array.Empty<(FengShuiElement, int)>(), Alpha = Seeded,
            ExpectedCurrent = new ElementVector(
                Tho: 0.187995m, Kim: 0.284947m, Thuy: 0.124947m, Moc: 0.083635m, Hoa: 0.318476m),
            Why = "Chỉ còn hai prior (nền phòng 3 phiếu + chủ nhân 2 phiếu). Không hành nào = 0, "
                + "và nén không làm vỡ gì khi chưa có bằng chứng nào.",
        });

        return data;
    }

    // ===================== B. Bất biến — chạy trên CÙNG bộ ca ở trên =====================

    /// <summary>
    /// Mọi tính chất §17 phải đúng ở mọi ca. Gom vào một hàm để thêm một dòng dữ liệu là tự động
    /// được kiểm cả năm, thay vì nhớ viết thêm hàm test cho từng ca mới.
    /// </summary>
    [Theory(DisplayName = "SCORE-SAT-INV")]
    [MemberData(nameof(Cases))]
    public void Saturation_HoldsEveryInvariant(SaturationCase c)
    {
        var linear = Build(c, 1m);
        var saturated = Build(c, Seeded);

        // (1) α=1 là mô hình tuyến tính thuần: current = khối lượng thô đã chuẩn hoá.
        Assert.Equal(Rounded(linear.RawMass.Normalize()), Rounded(linear.Current));

        // (2) Nén là phép ĐƠN ĐIỆU — không được đổi thứ tự các hành. Đây là ranh giới phân biệt nó
        //     với trần cứng: trần cứng làm phẳng mọi thứ vượt ngưỡng, 20 tag và 200 tag trông y hệt.
        static List<FengShuiElement> Order(ElementVector v)
            => v.Enumerate().OrderByDescending(x => x.Value).Select(x => x.Element).ToList();
        Assert.Equal(Order(linear.Current), Order(saturated.Current));

        foreach (var breakdown in new[] { linear, saturated })
        {
            // (3) Phần các nguồn phải cộng đúng ra current[e], nếu không FE xếp chồng lên radar sẽ hụt.
            foreach (var (element, value) in breakdown.Current.Enumerate())
            {
                decimal summed = breakdown.Contributions.Sum(x => breakdown.ShareOf(x, element));
                Assert.Equal(Math.Round(value, 6), Math.Round(summed, 6));
            }
            // Ở mức HIỂN THỊ thì chỉ đòi được xấp xỉ: `sharePercent` làm tròn 2 chữ số cho TỪNG dòng,
            // nên tổng trôi tối đa 0.005 mỗi nguồn (phòng 19 nguồn ra 100.1%). Trôi này có từ trước
            // §17 — công thức cũ cũng làm tròn từng dòng. Bất biến THẬT là đẳng thức chưa làm tròn ở
            // ngay trên; ràng buộc ở đây chỉ để chặn sai số vượt khỏi mức làm tròn giải thích được.
            var rows = CurrentBreakdownMapping.ToContributionRows(breakdown);
            Assert.True(Math.Abs(rows.Sum(r => r.SharePercent) - 100m) <= 0.005m * rows.Count,
                $"{c.Id}: Σ sharePercent = {rows.Sum(r => r.SharePercent)} với {rows.Count} nguồn — "
                + "lệch quá mức làm tròn từng dòng có thể giải thích.");
        }

        // (4) Tính chất khiến "áp lên tổng" thắng hai phương án "chỉ nén một nhóm nguồn": tỉ lệ giữa
        //     các nguồn TRONG cùng một hành không đổi. Nén chỉ ép tương phản GIỮA các hành, nên câu
        //     chuyện "prior loãng dần khi user khai thêm tag" của §12 sống nguyên vẹn.
        foreach (var element in Enum.GetValues<FengShuiElement>())
        {
            var factors = linear.Contributions
                .Where(x => x.Vector[element] > 0m && linear.ShareOf(x, element) > 0m)
                .Select(x => Math.Round(saturated.ShareOf(x, element) / linear.ShareOf(x, element), 6))
                .Distinct()
                .ToList();
            Assert.True(factors.Count <= 1,
                $"{c.Id}: hành {element} — các nguồn bị nhân những hệ số khác nhau ({string.Join(", ", factors)}).");
        }

        // (5) confidence đo CÓ BAO NHIÊU bằng chứng, không đo NHÌN THẤY ĐẬM tới đâu ⇒ tính trên phiếu
        //     thô, α không được nhúc nhích nó. Ngược lại thì độ tin cậy đổi dù user không khai thêm gì.
        Assert.Equal(
            CurrentBreakdownMapping.ConfidenceOf(linear),
            CurrentBreakdownMapping.ConfidenceOf(saturated));
    }

    /// <summary>
    /// <b>Bất biến theo tỉ lệ</b> — lý do chọn luỹ thừa thay vì <c>log</c>. Khai đúng căn phòng đó
    /// nhưng chi tiết gấp đôi (mọi nguồn ×2) phải ra <b>cùng một hình</b>. Với <c>log</c> thì không:
    /// người khai kỹ hơn bị đẩy về phía cân bằng, tức bị phạt vì cẩn thận.
    /// </summary>
    [Theory(DisplayName = "SCORE-SAT-SCALE")]
    [MemberData(nameof(Cases))]
    public void Saturation_IsScaleInvariant(SaturationCase c)
    {
        foreach (decimal alpha in new[] { 1m, Seeded })
            Assert.Equal(
                Rounded(Build(c, alpha).Current),
                Rounded(Build(c, alpha, scale: 2m).Current));
    }

    // ===================== C. Tham số phải khớp seed =====================

    /// <summary>
    /// Default trong code phải khớp <c>seed-data/scoring-params.json</c>: lệch nhau thì môi trường
    /// thiếu row sẽ chấm khác prod — đúng vết <c>PERSONAL_WEIGHT_*</c> đã vấp.
    /// </summary>
    [Fact(DisplayName = "SCORE-PARAM-03 [Normal] The saturation default matches the seeded value")]
    public void ScoringParameters_SaturationDefault_MatchesSeed()
        => Assert.Equal(Seeded, ScoringParameters.Default.EvidenceSaturationAlpha);
}

/// <summary>Một ca của §17 — xem <see cref="EvidenceSaturationTests"/>.</summary>
public sealed class SaturationCase
{
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    /// <summary>Tag user khai, mỗi tag 1 phiếu trỏ trọn vào một hành.</summary>
    public (FengShuiElement Element, int Count)[] Tags { get; init; } = Array.Empty<(FengShuiElement, int)>();

    /// <summary>Phiếu của chủ nhân phòng — mặc định 2, đúng mức seed của scope <c>Shared</c>.</summary>
    public decimal PersonVotes { get; init; } = 2m;

    public decimal Alpha { get; init; } = 1m;

    /// <summary><c>current</c> kỳ vọng, tính tay từ <c>normalize(m^α)</c>.</summary>
    public ElementVector ExpectedCurrent { get; init; }

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
