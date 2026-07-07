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
    {
        if (inputs.Count > 0)
            return ElementVector.FromContributions(
                resolver.ResolveMany(inputs.Select(i => (i.InputKind, i.InputCode))));

        return ElementVector.FromContributions(interiorFallback
            .Where(e => string.Equals(e.Source, WorkspaceElementSources.Interior, StringComparison.OrdinalIgnoreCase))
            .Select(e => new KeyValuePair<FengShuiElement, decimal>(e.Element, e.Weight)));
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
