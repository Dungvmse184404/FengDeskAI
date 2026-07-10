using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.Catalog.Services;

/// <summary>
/// Áp input (màu/vật liệu/hình khối) lên 1 product + tính lại cache vector (tầng 2).
/// Dùng chung cho <see cref="ProductVectorService.SetElementInputsAsync"/> và <see cref="ProductService.CreateAsync"/>
/// để hai đường (tạo mới / sửa sau) không lệch hành vi.
/// </summary>
internal static class ProductVectorApplier
{
    /// <summary>Trả về lỗi validate (codes lạ) hoặc null, kèm các entity đã áp. KHÔNG SaveChanges — caller commit.</summary>
    public static async Task<(string? Error, List<ProductElementInput> Entities)> ApplyInputsAsync(
        Product product, IReadOnlyList<(ElementInputKind Kind, string Code)> inputs,
        IUnitOfWork uow, CancellationToken ct)
    {
        var map = await uow.ScoringConfig.GetElementInputMapAsync(ct);
        var validCodes = map.Select(m => (m.InputKind, Code: m.InputCode)).ToHashSet();

        var cleaned = inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.Code))
            .Distinct()
            .ToList();

        var unknown = cleaned.Where(i => !validCodes.Contains(i)).ToList();
        if (unknown.Count > 0)
            return ($"Tín hiệu không có trong bảng element_input_map: {string.Join(", ", unknown.Select(u => $"{u.Kind}:{u.Code}"))}.", new());

        var entities = cleaned.Select(i => new ProductElementInput { InputKind = i.Kind, InputCode = i.Code }).ToList();
        await uow.ScoringConfig.ReplaceProductElementInputsAsync(product.Id, entities, ct);

        // Khai input thủ công → tắt override, tính lại cache vector (tầng 2). Rỗng → xóa cache (về tầng 3 lúc chấm).
        product.IsVectorOverridden = false;
        if (entities.Count == 0)
        {
            ClearVectorColumns(product);
        }
        else
        {
            var prms = ScoringParameters.FromRows(await uow.ScoringConfig.GetScoringParamsAsync(ct));
            var resolver = new ElementInputResolver(map);
            var vector = ProductVectorProvider.Build(
                isOverridden: false, overriddenVector: null, inputs: entities, resolver: resolver,
                productElements: product.Elements.Select(e => (e.Element, e.IsPrimary)), p: prms);
            ApplyVectorColumns(product, vector);
        }

        return (null, entities);
    }

    private static void ApplyVectorColumns(Product p, ElementVector v)
    {
        p.ElementTho = v.Tho;
        p.ElementKim = v.Kim;
        p.ElementThuy = v.Thuy;
        p.ElementMoc = v.Moc;
        p.ElementHoa = v.Hoa;
    }

    private static void ClearVectorColumns(Product p)
    {
        p.ElementTho = p.ElementKim = p.ElementThuy = p.ElementMoc = p.ElementHoa = null;
    }
}
