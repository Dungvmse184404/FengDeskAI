using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Engine;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Recommendation;

namespace FengDeskAI.Application.Features.Catalog.Services;

public sealed class ProductVectorService : IProductVectorService
{
    private readonly IUnitOfWork _uow;

    public ProductVectorService(IUnitOfWork uow) => _uow = uow;

    public async Task<IServiceResult<ProductVectorResponse>> GetAsync(Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var (error, product) = await GuardAsync(productId, userId, isAdmin, ct);
        if (error is not null) return error;

        var inputs = await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct);
        return ServiceResult<ProductVectorResponse>.Success(ToResponse(product!, inputs));
    }

    public async Task<IServiceResult<ProductVectorResponse>> SetElementInputsAsync(
        Guid productId, Guid userId, bool isAdmin, SetProductElementInputsRequest request, CancellationToken ct = default)
    {
        var (error, product) = await GuardAsync(productId, userId, isAdmin, ct);
        if (error is not null) return error;

        var map = await _uow.ScoringConfig.GetElementInputMapAsync(ct);
        var validCodes = map.Select(m => (m.InputKind, Code: m.InputCode)).ToHashSet();

        var cleaned = request.Inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.Code))
            .Select(i => (i.Kind, i.Code))
            .Distinct()
            .ToList();

        var unknown = cleaned.Where(i => !validCodes.Contains(i)).ToList();
        if (unknown.Count > 0)
            return ServiceResult<ProductVectorResponse>.Failure(ApiStatusCodes.BadRequest,
                $"Tín hiệu không có trong bảng element_input_map: {string.Join(", ", unknown.Select(u => $"{u.Kind}:{u.Code}"))}.");

        var entities = cleaned.Select(i => new ProductElementInput { InputKind = i.Kind, InputCode = i.Code }).ToList();
        await _uow.ScoringConfig.ReplaceProductElementInputsAsync(productId, entities, ct);

        // Khai input thủ công → tắt override, tính lại cache vector (tầng 2). Rỗng → xóa cache (về tầng 3 lúc chấm).
        product!.IsVectorOverridden = false;
        if (entities.Count == 0)
        {
            ClearVectorColumns(product);
        }
        else
        {
            var prms = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));
            var resolver = new ElementInputResolver(map);
            var vector = ProductVectorProvider.Build(
                isOverridden: false, overriddenVector: null, inputs: entities, resolver: resolver,
                productElements: product.Elements.Select(e => (e.Element, e.IsPrimary)), p: prms);
            ApplyVectorColumns(product, vector);
        }

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductVectorResponse>.Success(ToResponse(product, entities), "Cập nhật input ngũ hành sản phẩm thành công.");
    }

    public async Task<IServiceResult<ProductVectorResponse>> SetVectorOverrideAsync(
        Guid productId, Guid userId, bool isAdmin, SetProductVectorOverrideRequest request, CancellationToken ct = default)
    {
        var (error, product) = await GuardAsync(productId, userId, isAdmin, ct);
        if (error is not null) return error;

        var raw = new ElementVector(request.Tho, request.Kim, request.Thuy, request.Moc, request.Hoa);
        if (raw.Enumerate().Any(x => x.Value < 0m))
            return ServiceResult<ProductVectorResponse>.Failure(ApiStatusCodes.BadRequest, "Vector không được có giá trị âm.");
        if (raw.L1() <= 0m)
            return ServiceResult<ProductVectorResponse>.Failure(ApiStatusCodes.BadRequest, "Tổng vector phải > 0.");
        if (Math.Abs(raw.L1() - 1m) > 0.02m)
            return ServiceResult<ProductVectorResponse>.Failure(ApiStatusCodes.BadRequest, "Tổng 5 hành phải xấp xỉ 1.0 (sai số ±0.02).");

        ApplyVectorColumns(product!, raw.Normalize());
        product!.IsVectorOverridden = true;

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        var inputs = await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct);
        return ServiceResult<ProductVectorResponse>.Success(ToResponse(product, inputs), "Ghi đè vector ngũ hành thành công.");
    }

    public async Task<IServiceResult<ProductVectorResponse>> ClearVectorOverrideAsync(
        Guid productId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var (error, product) = await GuardAsync(productId, userId, isAdmin, ct);
        if (error is not null) return error;

        product!.IsVectorOverridden = false;

        // Tính lại từ input hiện có (nếu còn), không thì xóa cache về tầng 3.
        var inputs = await _uow.ScoringConfig.GetProductElementInputsAsync(new[] { productId }, ct);
        if (inputs.Count > 0)
        {
            var prms = ScoringParameters.FromRows(await _uow.ScoringConfig.GetScoringParamsAsync(ct));
            var resolver = new ElementInputResolver(await _uow.ScoringConfig.GetElementInputMapAsync(ct));
            var vector = ProductVectorProvider.Build(
                false, null, inputs, resolver,
                product.Elements.Select(e => (e.Element, e.IsPrimary)), prms);
            ApplyVectorColumns(product, vector);
        }
        else
        {
            ClearVectorColumns(product);
        }

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductVectorResponse>.Success(ToResponse(product, inputs), "Đã bỏ ghi đè vector.");
    }

    // ── helpers ──

    private async Task<(ServiceResult<ProductVectorResponse>? Error, Product? Product)> GuardAsync(
        Guid productId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var product = await _uow.Products.GetForUpdateAsync(productId, ct);
        if (product is null)
            return (ServiceResult<ProductVectorResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm."), null);
        if (!isAdmin && !await _uow.Stores.CanManageAsync(product.GardenStoreId, userId, ct))
            return (ServiceResult<ProductVectorResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền quản lý sản phẩm này."), null);
        return (null, product);
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

    private static ProductVectorResponse ToResponse(Product p, IReadOnlyCollection<ProductElementInput> inputs) => new()
    {
        ProductId = p.Id,
        IsVectorOverridden = p.IsVectorOverridden,
        Tho = p.ElementTho,
        Kim = p.ElementKim,
        Thuy = p.ElementThuy,
        Moc = p.ElementMoc,
        Hoa = p.ElementHoa,
        Inputs = inputs.Select(i => new ProductElementInputDto { Kind = i.InputKind, Code = i.InputCode }).ToList(),
    };
}
