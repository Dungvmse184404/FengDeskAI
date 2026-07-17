using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Engine;

/// <summary>
/// Tra cứu <c>element_input_map</c>: một tín hiệu (kind, code) → các đóng góp (hành, trọng số).
/// Dựng 1 lần từ toàn bộ bảng map rồi dùng chung cho phòng &amp; sản phẩm.
/// </summary>
public sealed class ElementInputResolver
{
    private readonly Dictionary<(ElementInputKind, string), List<KeyValuePair<FengShuiElement, decimal>>> _map;

    public ElementInputResolver(IEnumerable<ElementInputMap> rows)
    {
        _map = new();
        foreach (var r in rows)
        {
            var key = (r.InputKind, r.InputCode);
            if (!_map.TryGetValue(key, out var list))
                _map[key] = list = new();
            list.Add(new(r.Element, r.Weight));
        }
    }

    public IEnumerable<KeyValuePair<FengShuiElement, decimal>> Resolve(ElementInputKind kind, string code)
        => _map.TryGetValue((kind, code), out var list)
            ? list
            : Enumerable.Empty<KeyValuePair<FengShuiElement, decimal>>();

    public IEnumerable<KeyValuePair<FengShuiElement, decimal>> ResolveMany(
        IEnumerable<(ElementInputKind Kind, string Code)> inputs)
        => inputs.SelectMany(i => Resolve(i.Kind, i.Code));
}

/// <summary>PHẦN C.2 — dựng vector phòng: ideal → bẻ theo intent → hiện trạng.</summary>
public static class WorkspaceVectorBuilder
{
    /// <summary>Vector lý tưởng từ các row <c>Source == "Ideal"</c> (chuẩn hóa Σ=1).</summary>
    public static ElementVector BuildIdeal(IEnumerable<WorkspaceTypeElement> typeElements)
        => ElementVector.FromContributions(typeElements
            .Where(e => string.Equals(e.Source, WorkspaceElementSources.Ideal, StringComparison.OrdinalIgnoreCase))
            .Select(e => new KeyValuePair<FengShuiElement, decimal>(e.Element, e.Weight)));

    /// <summary>Bẻ ideal theo Intent (delta có thể âm) rồi chuẩn hóa lại.</summary>
    public static ElementVector ApplyIntent(ElementVector ideal, IEnumerable<WorkPurposeElementModifier> modifiers)
    {
        var adjusted = ideal;
        foreach (var m in modifiers)
            adjusted = adjusted.Add(ElementVector.Single(m.Element).Scale(m.Delta));
        return adjusted.Normalize();
    }

    /// <summary>
    /// Hiện trạng phòng: nếu user khai màu/vật liệu → cộng theo map; nếu không → fallback vector
    /// Interior mặc định theo loại phòng.
    /// </summary>
    public static ElementVector BuildCurrent(
        IReadOnlyCollection<WorkspaceProfileInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<WorkspaceTypeElement> interiorFallback)
        => BuildCurrentWithProducts(inputs, resolver, interiorFallback,
            Array.Empty<(ElementVector, decimal)>());

    /// <summary>
    /// Số "phiếu" quy ước cho vector Interior fallback khi phòng KHÔNG khai input nào —
    /// coi nội thất mặc định như phòng trung bình 5 món đồ, để 1 sản phẩm đặt vào không chiếm 50%.
    /// </summary>
    public const decimal InteriorFallbackVotes = 5m;

    /// <summary>
    /// Hiện trạng phòng + SẢN PHẨM ĐÃ MUA đặt vào (tính lúc đọc, không lưu):
    /// mỗi input user khai = 1 phiếu; mỗi sản phẩm = vector chuẩn hóa × voteWeight
    /// (mặc định 1.0, scale theo DecorItem code trong element_input_map).
    /// Phòng không khai input → Interior fallback được scale thành <see cref="InteriorFallbackVotes"/> phiếu.
    /// </summary>
    public static ElementVector BuildCurrentWithProducts(
        IReadOnlyCollection<WorkspaceProfileInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<WorkspaceTypeElement> interiorFallback,
        IReadOnlyCollection<(ElementVector Vector, decimal VoteWeight)> productContributions)
    {
        // Vector nền cộng THÔ (không chuẩn hóa vội) — giữ nguyên tổng "phiếu" để sản phẩm
        // cộng vào đúng tỉ lệ. Lưu ý FromContributions tự Normalize nên KHÔNG dùng ở đây.
        ElementVector baseVector;
        if (inputs.Count > 0)
        {
            // Mỗi input ≈ 1 phiếu (mỗi code trong element_input_map có Σ weight ≈ 1;
            // nếu admin giảm weight của code thì phiếu của input đó tự giảm theo — chủ đích).
            baseVector = RawSum(resolver.ResolveMany(inputs.Select(i => (i.InputKind, i.InputCode))));
        }
        else
        {
            // Interior fallback (Σ=1) scale thành N phiếu quy ước.
            baseVector = RawSum(interiorFallback
                    .Where(e => string.Equals(e.Source, WorkspaceElementSources.Interior, StringComparison.OrdinalIgnoreCase))
                    .Select(e => new KeyValuePair<FengShuiElement, decimal>(e.Element, e.Weight)))
                .Normalize()
                .Scale(InteriorFallbackVotes);
        }

        foreach (var (vector, voteWeight) in productContributions)
        {
            if (voteWeight <= 0m) continue;
            baseVector = baseVector.Add(vector.Normalize().Scale(voteWeight));
        }

        return baseVector.Normalize();
    }

    /// <summary>Cộng dồn contributions KHÔNG chuẩn hóa (khác <see cref="ElementVector.FromContributions"/>).</summary>
    private static ElementVector RawSum(IEnumerable<KeyValuePair<FengShuiElement, decimal>> contributions)
    {
        var v = ElementVector.Zero;
        foreach (var c in contributions)
            v = v.Add(ElementVector.Single(c.Key).Scale(c.Value));
        return v;
    }
}

/// <summary>Hằng cho cột <c>workspace_type_elements.source</c>.</summary>
public static class WorkspaceElementSources
{
    public const string Ideal = "Ideal";
    public const string Interior = "Interior";
}

/// <summary>PHẦN C.3 — dựng vector sản phẩm với 3 tầng nguồn dữ liệu (fallback dần).</summary>
public static class ProductVectorProvider
{
    /// <summary>
    /// Tầng 1: override thủ công → dùng vector nhập tay.
    /// Tầng 2: có product_element_inputs → chất liệu (MATERIAL_SHARE) + màu/hình (COLOR_SHARE).
    /// Tầng 3: backfill từ product_elements (primary/secondary theo FALLBACK_*).
    /// </summary>
    public static ElementVector Build(
        bool isOverridden,
        ElementVector? overriddenVector,
        IReadOnlyCollection<ProductElementInput> inputs,
        ElementInputResolver resolver,
        IEnumerable<(FengShuiElement Element, bool IsPrimary)> productElements,
        ScoringParameters p)
    {
        // Tầng 1
        if (isOverridden && overriddenVector is { } ov)
            return ov.Normalize();

        // Tầng 1.5 — sản phẩm được gắn "loại vật trang trí" (DecorItem, vd SaltLamp): dùng THẲNG
        // contributions của code đó trong element_input_map → đồng bộ tuyệt đối với tag hiện trạng
        // workspace cùng tên (chỉnh weight 1 chỗ trong seed-data/element-input-map.json là cả hai đổi).
        var decorInputs = inputs.Where(i => i.InputKind == ElementInputKind.DecorItem).ToList();
        if (decorInputs.Count > 0)
        {
            var decorVector = ElementVector.FromContributions(
                resolver.ResolveMany(decorInputs.Select(i => (i.InputKind, i.InputCode))));
            if (decorVector.L1() > 0m) return decorVector.Normalize();
        }

        // Tầng 2
        if (inputs.Count > 0)
        {
            var materialVector = ElementVector.FromContributions(resolver.ResolveMany(
                inputs.Where(i => i.InputKind == ElementInputKind.Material).Select(i => (i.InputKind, i.InputCode))));
            var surfaceVector = ElementVector.FromContributions(resolver.ResolveMany(
                inputs.Where(i => i.InputKind is ElementInputKind.Color or ElementInputKind.Shape)
                      .Select(i => (i.InputKind, i.InputCode))));

            return materialVector.Scale(p.MaterialShare)
                .Add(surfaceVector.Scale(p.ColorShare))
                .Normalize();
        }

        // Tầng 3 — backfill
        var elements = productElements.ToList();
        var primary = elements.Where(e => e.IsPrimary).Select(e => (FengShuiElement?)e.Element).FirstOrDefault();
        var secondary = elements.Where(e => !e.IsPrimary).Select(e => (FengShuiElement?)e.Element).FirstOrDefault();

        if (primary is not { } prim)
            return ElementVector.Zero;

        var contrib = new List<KeyValuePair<FengShuiElement, decimal>>
        {
            new(prim, secondary is null ? 1.0m : p.FallbackPrimary),
        };
        if (secondary is { } sec)
            contrib.Add(new(sec, p.FallbackSecondary));

        return ElementVector.FromContributions(contrib);
    }
}
