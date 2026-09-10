using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Entities.Workspace;
using FengDeskAI.Domain.Enums.Workspace;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>
/// §19 — <c>current</c> chia theo NGÂN SÁCH TỈ TRỌNG của ba khối nguồn thay vì theo phiếu.
/// Data-Driven: mỗi ca là một <see cref="BudgetCase"/>.
///
/// <code>
/// evidence[e] = Σ (phiếu_i / Σ phiếu khối) · w_i[e]     // tag + sản phẩm đã đặt
/// m[e]        = B_nền·interior[e] + B_mệnh·person[e] + B_bằngchứng·evidence[e]
/// current     = normalize(m^α)
/// </code>
///
/// <para>
/// Vì sao bỏ mô hình phiếu của §12: khối bằng chứng nuốt dần hai prior — khai 20 tag thì nền phòng
/// còn 12% và bản mệnh còn 8%, tức người dùng càng chăm khai càng tự xoá bản mệnh của mình khỏi phân
/// tích. Đánh đổi đã biết: một tag duy nhất nay gánh trọn 30% thay vì 1/6.
/// </para>
///
/// <para>Kỳ vọng tính TAY từ công thức trên, không lấy ngược từ code.</para>
/// </summary>
public sealed class ElementBudgetTests
{
    private static readonly Guid KitchenId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private const decimal Alpha = 0.60m;

    /// <summary>Nhà bếp chính — nền phòng nghiêng hẳn về Hỏa.</summary>
    private static readonly (FengShuiElement Element, decimal Weight)[] Interior =
    {
        (FengShuiElement.Hoa, 0.65m), (FengShuiElement.Kim, 0.14m), (FengShuiElement.Moc, 0.07m),
        (FengShuiElement.Tho, 0.07m), (FengShuiElement.Thuy, 0.07m),
    };

    /// <summary>6 tag thật của hồ sơ: tranh · gỗ · vàng · đá · trắng · bạc.</summary>
    private static readonly (FengShuiElement Element, decimal Weight)[][] Tags =
    {
        new[] { (FengShuiElement.Hoa, 0.5m), (FengShuiElement.Moc, 0.5m) },
        new[] { (FengShuiElement.Moc, 1m) },
        new[] { (FengShuiElement.Tho, 1m) },
        new[] { (FengShuiElement.Tho, 1m) },
        new[] { (FengShuiElement.Kim, 1m) },
        new[] { (FengShuiElement.Kim, 1m) },
    };

    private static PersonPresence KimOwner => new(
        "Bạn — mệnh Kim", 2m,
        new ElementVector(Tho: 0.3m, Kim: 0.6m, Thuy: 0.1m, Moc: 0m, Hoa: 0m));

    // ===================== Bộ chạy chung =====================

    private static CurrentBreakdown Build(BudgetCase c)
    {
        var map = new List<ElementInputMap>();
        var inputs = new List<WorkspaceProfileInput>();
        if (c.WithTags)
        {
            for (int i = 0; i < Tags.Length; i++)
            {
                string code = $"TAG{i}";
                map.AddRange(Tags[i].Select(x => new ElementInputMap
                {
                    InputKind = ElementInputKind.DecorItem, InputCode = code, LabelVi = code,
                    Element = x.Element, Weight = x.Weight,
                }));
                inputs.Add(new WorkspaceProfileInput { InputKind = ElementInputKind.DecorItem, InputCode = code });
            }
        }

        return WorkspaceVectorBuilder.BuildCurrentBreakdown(
            inputs,
            new ElementInputResolver(map),
            Interior.Select(x => new WorkspaceTypeElement
            {
                WorkspaceTypeId = KitchenId, Element = x.Element, Weight = x.Weight,
                Source = WorkspaceElementSources.Interior,
            }).ToList(),
            Array.Empty<ProductContribution>(),
            c.WithPerson ? KimOwner : null,
            interiorVotes: 3m,
            saturationAlpha: Alpha,
            budgetParams: ScoringParameters.Default,
            scope: c.Scope);
    }

    private static ElementVector Rounded(ElementVector v, int digits = 6) => new(
        Math.Round(v.Tho, digits), Math.Round(v.Kim, digits), Math.Round(v.Thuy, digits),
        Math.Round(v.Moc, digits), Math.Round(v.Hoa, digits));

    // ===================== A. Giá trị chính xác =====================

    [Theory(DisplayName = "SCORE-BUD")]
    [MemberData(nameof(Cases))]
    public void ElementBudget_ProducesExpectedCurrent(BudgetCase c)
        => Assert.Equal(Rounded(c.ExpectedCurrent), Rounded(Build(c).Current));

    public static TheoryData<BudgetCase> Cases()
    {
        var data = new TheoryData<BudgetCase>();

        data.Add(new BudgetCase
        {
            Id = "SCORE-BUD-01", Name = "[Normal] Shared splits 40 interior / 30 destiny / 30 evidence",
            Scope = WorkspaceScope.Shared, WithTags = true, WithPerson = true,
            ExpectedCurrent = new ElementVector(
                Tho: 0.219280m, Kim: 0.284269m, Thuy: 0.099079m, Moc: 0.139839m, Hoa: 0.257533m),
            Why = "6 tag chia đều 30%; nền 40% kéo Hỏa lên, bản mệnh 30% kéo Kim lên.",
        });

        data.Add(new BudgetCase
        {
            Id = "SCORE-BUD-02", Name = "[Normal] Private gives the destiny the largest share",
            Scope = WorkspaceScope.Private, WithTags = true, WithPerson = true,
            ExpectedCurrent = new ElementVector(
                Tho: 0.233685m, Kim: 0.308076m, Thuy: 0.102475m, Moc: 0.134519m, Hoa: 0.221245m),
            Why = "30/40/30 — phòng riêng thì chủ nhân nặng nhất, Kim 28.4% → 30.8% so với Shared.",
        });

        data.Add(new BudgetCase
        {
            Id = "SCORE-BUD-03", Name = "[Boundary] A public room carries no destiny at all",
            Scope = WorkspaceScope.Public, WithTags = true, WithPerson = true,
            ExpectedCurrent = new ElementVector(
                Tho: 0.194554m, Kim: 0.221308m, Thuy: 0.082541m, Moc: 0.171433m, Hoa: 0.330165m),
            Why = "60/0/40 — lễ tân không thuộc về ai (Q12). Dù có truyền chủ nhân vào, phần của họ "
                + "vẫn phải bằng 0; Kim tụt còn 22.1% và Hỏa của nền phòng nổi lên 33.0%.",
        });

        data.Add(new BudgetCase
        {
            Id = "SCORE-BUD-04", Name = "[Boundary] A room with nothing declared falls back to 60/40",
            Scope = WorkspaceScope.Shared, WithTags = false, WithPerson = true,
            ExpectedCurrent = new ElementVector(
                Tho: 0.187995m, Kim: 0.284947m, Thuy: 0.124947m, Moc: 0.083635m, Hoa: 0.318476m),
            Why = "Không có gì đổ vào khối bằng chứng ⇒ 60 nền / 40 bản mệnh, KHÔNG phải 40/30 chuẩn "
                + "hoá thành 57/43 — hai tỉ lệ khác nhau nên phải là luật riêng.",
        });

        data.Add(new BudgetCase
        {
            Id = "SCORE-BUD-05", Name = "[Abnormal] A public room with nothing declared is all interior",
            Scope = WorkspaceScope.Public, WithTags = false, WithPerson = true,
            ExpectedCurrent = new ElementVector(
                Tho: 0.120140m, Kim: 0.182098m, Thuy: 0.120140m, Moc: 0.120140m, Hoa: 0.457484m),
            Why = "Bản mệnh đã 0 vì Public, khối bằng chứng rỗng ⇒ chỉ còn nền phòng 100%. Luật 60/40 "
                + "KHÔNG được áp ở đây, nếu không lễ tân lại mọc ra 40% bản mệnh của một người.",
        });

        return data;
    }

    // ===================== B. Bất biến =====================

    /// <summary>
    /// Tính chất §19 tồn tại để có: khai thêm tag KHÔNG được làm loãng hai prior nữa. Đây là điều mô
    /// hình phiếu không giữ được (20 tag ⇒ nền còn 12%, bản mệnh còn 8%).
    /// </summary>
    [Theory(DisplayName = "SCORE-BUD-06 [Normal] Declaring more tags never dilutes the two priors")]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(20)]
    public void MoreTags_DoNotDiluteInteriorOrPerson(int tagCount)
    {
        var map = new List<ElementInputMap>();
        var inputs = new List<WorkspaceProfileInput>();
        for (int i = 0; i < tagCount; i++)
        {
            string code = $"T{i}";
            map.Add(new ElementInputMap
            {
                InputKind = ElementInputKind.DecorItem, InputCode = code, LabelVi = code,
                Element = FengShuiElement.Moc, Weight = 1m,
            });
            inputs.Add(new WorkspaceProfileInput { InputKind = ElementInputKind.DecorItem, InputCode = code });
        }

        var breakdown = WorkspaceVectorBuilder.BuildCurrentBreakdown(
            inputs, new ElementInputResolver(map),
            Interior.Select(x => new WorkspaceTypeElement
            {
                WorkspaceTypeId = KitchenId, Element = x.Element, Weight = x.Weight,
                Source = WorkspaceElementSources.Interior,
            }).ToList(),
            Array.Empty<ProductContribution>(), KimOwner, 3m, Alpha,
            ScoringParameters.Default, WorkspaceScope.Shared);

        var interior = breakdown.Contributions.Single(c => c.Source == CurrentSourceKind.Interior);
        var person = breakdown.Contributions.Single(c => c.Source == CurrentSourceKind.Person);

        Assert.Equal(0.40m, interior.Weight);
        Assert.Equal(0.30m, person.Weight);
        Assert.Equal(0.30m, Math.Round(
            breakdown.Contributions.Where(c => c.Source == CurrentSourceKind.Tag).Sum(c => c.Weight), 6));

        // Phiếu vẫn là phiếu — `confidence` đọc nó, và nó vẫn phải tăng theo số tag user khai.
        Assert.Equal(tagCount, breakdown.Contributions.Count(c => c.Source == CurrentSourceKind.Tag));
    }

    /// <summary>
    /// <c>confidence</c> đo CÓ BAO NHIÊU bằng chứng, không đo bằng chứng NẶNG bao nhiêu trong công
    /// thức. §19 khoá trọng số lại ở 30%, nhưng độ tin cậy vẫn phải tăng khi user khai thêm — nếu
    /// không, phòng khai 1 tag và phòng khai 20 tag sẽ báo cùng một mức tin cậy.
    /// </summary>
    [Fact(DisplayName = "SCORE-BUD-07 [Normal] Confidence still rises with real evidence, though its weight is capped")]
    public void Confidence_StillGrowsWithEvidence_EvenThoughWeightIsFixed()
    {
        decimal Confidence(int tagCount)
        {
            var map = new List<ElementInputMap>();
            var inputs = new List<WorkspaceProfileInput>();
            for (int i = 0; i < tagCount; i++)
            {
                string code = $"T{i}";
                map.Add(new ElementInputMap
                {
                    InputKind = ElementInputKind.DecorItem, InputCode = code, LabelVi = code,
                    Element = FengShuiElement.Moc, Weight = 1m,
                });
                inputs.Add(new WorkspaceProfileInput { InputKind = ElementInputKind.DecorItem, InputCode = code });
            }

            return CurrentBreakdownMapping.ConfidenceOf(WorkspaceVectorBuilder.BuildCurrentBreakdown(
                inputs, new ElementInputResolver(map),
                Interior.Select(x => new WorkspaceTypeElement
                {
                    WorkspaceTypeId = KitchenId, Element = x.Element, Weight = x.Weight,
                    Source = WorkspaceElementSources.Interior,
                }).ToList(),
                Array.Empty<ProductContribution>(), KimOwner, 3m, Alpha,
                ScoringParameters.Default, WorkspaceScope.Shared));
        }

        Assert.True(Confidence(1) < Confidence(6));
        Assert.True(Confidence(6) < Confidence(20));
    }

    /// <summary>Ngân sách phải Σ = 1 ở mọi tổ hợp, nếu không <c>current</c> lệch thang một cách âm thầm.</summary>
    [Theory(DisplayName = "SCORE-BUD-08 [Boundary] The three shares always add up to one")]
    [MemberData(nameof(BudgetCombinations))]
    public void ElementBudget_AlwaysSumsToOne(WorkspaceScope scope, bool hasPerson, bool hasEvidence)
    {
        var budget = ScoringParameters.Default.ElementBudgetFor(scope, hasPerson, hasEvidence);

        Assert.Equal(1m, Math.Round(budget.Interior + budget.Person + budget.Evidence, 6));
        if (!hasPerson || scope == WorkspaceScope.Public) Assert.Equal(0m, budget.Person);
        if (!hasEvidence) Assert.Equal(0m, budget.Evidence);
    }

    public static TheoryData<WorkspaceScope, bool, bool> BudgetCombinations()
    {
        var data = new TheoryData<WorkspaceScope, bool, bool>();
        foreach (var scope in Enum.GetValues<WorkspaceScope>())
        foreach (var hasPerson in new[] { true, false })
        foreach (var hasEvidence in new[] { true, false })
            data.Add(scope, hasPerson, hasEvidence);
        return data;
    }
}

/// <summary>Một ca của §19 — xem <see cref="ElementBudgetTests"/>.</summary>
public sealed class BudgetCase
{
    public string Id { get; init; } = "";

    /// <summary>Tên hiển thị, gồm nhãn phân loại [Normal] / [Boundary] / [Abnormal].</summary>
    public string Name { get; init; } = "";

    public WorkspaceScope Scope { get; init; } = WorkspaceScope.Shared;
    public bool WithTags { get; init; } = true;
    public bool WithPerson { get; init; } = true;

    public ElementVector ExpectedCurrent { get; init; }

    /// <summary>Phép tính bằng tay — in ra khi ca fail.</summary>
    public string Why { get; init; } = "";

    public override string ToString() => $"{Id} {Name}";
}
