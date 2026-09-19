using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Media;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IFileStorage _storage;

    private readonly ISkuGenerator _skuGenerator;

    public ProductService(IUnitOfWork uow, IMapper mapper, IFileStorage storage, ISkuGenerator skuGenerator)
    {
        _uow = uow;
        _mapper = mapper;
        _storage = storage;
        _skuGenerator = skuGenerator;
    }

    public async Task<IServiceResult<PagedResult<ProductListItemResponse>>> SearchAsync(ProductQueryParams query, CancellationToken ct = default)
    {
        var filter = new ProductSearchFilter
        {
            StoreId = query.StoreId,
            CategoryId = query.CategoryId,
            Search = query.Search,
            Element = query.Element,
            Aspiration = query.Aspiration,
            Placement = query.Placement,
            HasModel3D = query.HasModel3D,
            ActiveOnly = true,
            Skip = query.Skip,
            Take = query.PageSize,
        };
        var (items, total) = await _uow.Products.SearchAsync(filter, ct);
        var result = new PagedResult<ProductListItemResponse>(
            _mapper.Map<List<ProductListItemResponse>>(items), query.Page, query.PageSize, total);
        return ServiceResult<PagedResult<ProductListItemResponse>>.Success(result);
    }

    public async Task<IServiceResult<ProductDetailResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetDetailAsync(id, ct);
        if (product is null)
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(product));
    }

    public async Task<IServiceResult<ProductDetailResponse>> CreateAsync(Guid userId, bool isAdmin, CreateProductRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.NameRequired);
        if (!await CanManageStoreAsync(request.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.CreateForbidden);
        if (!await _uow.Categories.AllExistAsync(request.CategoryIds, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.CategoriesNotExist);
        // Vibe/style độc lập với PrimaryElement (element là enum nên luôn hợp lệ) — kiểm code có trong bảng tra cứu,
        // cùng cách SetFengShuiAsync đang làm, để tránh lỗi FK 500 và trả 400 thân thiện.
        if (!await AllCodesExistAsync(_uow.Styles, request.Styles, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.StylesNotExist);
        if (!await AllCodesExistAsync(_uow.Vibes, request.Vibes, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.VibesNotExist);

        var product = new Product
        {
            GardenStoreId = request.GardenStoreId,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsActive = true,
        };
        foreach (var i in request.Items)
        {
            var sku = await ResolveSkuAsync(i.Sku, null, ct);
            if (sku.Error is not null)
                return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, sku.Error);

            product.Items.Add(new ProductItem { Name = i.Name, Price = i.Price, Stock = i.Stock, Sku = sku.Value, SizeClass = i.SizeClass, WeightGram = i.WeightGram, LengthCm = i.LengthCm, WidthCm = i.WidthCm, HeightCm = i.HeightCm });
        }
        foreach (var img in request.Images)
            product.Images.Add(new ProductImage { Url = img.Url, SortOrder = img.SortOrder });
        foreach (var cid in request.CategoryIds.Distinct())
            product.ProductCategories.Add(new ProductCategory { CategoryId = cid });
        ApplyFengShui(product, request);

        // Đường auto-calc (tầng 2) — ưu tiên hơn PrimaryElement, cùng 1 SaveChangesAsync nên atomic (lỗi validate → không tạo product).
        if (request.ElementInputs.Count > 0)
        {
            var inputs = request.ElementInputs.Select(i => (i.Kind, i.Code)).ToList();
            var (applyError, _) = await ProductVectorApplier.ApplyInputsAsync(product, inputs, _uow, ct);
            if (applyError is not null)
                return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, applyError);
        }

        await _uow.Products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);

        var detail = await _uow.Products.GetDetailAsync(product.Id, ct);
        return ServiceResult<ProductDetailResponse>.Success(
            _mapper.Map<ProductDetailResponse>(detail), ApiStatusMessages.Product.Created, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<ProductDetailResponse>> UpdateAsync(Guid id, Guid userId, bool isAdmin, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product is null)
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.UpdateForbidden);
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.NameRequired);

        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.IsActive = request.IsActive;
        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        var detail = await _uow.Products.GetDetailAsync(id, ct);
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(detail), ApiStatusMessages.Product.Updated);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.DeleteForbidden);
        _uow.Products.Remove(product);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Product.Deleted);
    }

    public async Task<IServiceResult<ProductItemResponse>> AddItemAsync(Guid productId, Guid userId, bool isAdmin, CreateProductItemRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductItemResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (request.Price < 0)
            return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.PriceInvalid);

        var sku = await ResolveSkuAsync(request.Sku, null, ct);
        if (sku.Error is not null)
            return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.BadRequest, sku.Error);

        var item = new ProductItem { ProductId = productId, Name = request.Name, Price = request.Price, Stock = request.Stock, Sku = sku.Value, SizeClass = request.SizeClass, WeightGram = request.WeightGram, LengthCm = request.LengthCm, WidthCm = request.WidthCm, HeightCm = request.HeightCm };
        await _uow.Products.AddItemAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductItemResponse>.Success(_mapper.Map<ProductItemResponse>(item), ApiStatusMessages.Product.ItemCreated, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<ProductItemResponse>> UpdateItemAsync(Guid productId, Guid itemId, Guid userId, bool isAdmin, UpdateProductItemRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductItemResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;

        var item = await _uow.Products.GetItemAsync(productId, itemId, ct);
        if (item is null) return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.ItemNotFound);
        if (request.Price < 0) return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.PriceInvalid);

        var sku = await ResolveSkuAsync(request.Sku, itemId, ct);
        if (sku.Error is not null)
            return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.BadRequest, sku.Error);

        item.Name = request.Name;
        item.Price = request.Price;
        item.Stock = request.Stock;
        item.Sku = sku.Value;
        item.SizeClass = request.SizeClass;
        item.WeightGram = request.WeightGram;
        item.LengthCm = request.LengthCm;
        item.WidthCm = request.WidthCm;
        item.HeightCm = request.HeightCm;
        // item được track bởi DbContext (GetItemAsync không AsNoTracking) → SaveChanges tự persist
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductItemResponse>.Success(_mapper.Map<ProductItemResponse>(item), ApiStatusMessages.Product.ItemUpdated);
    }

    public async Task<IServiceResult> DeleteItemAsync(Guid productId, Guid itemId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<object>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        var item = await _uow.Products.GetItemAsync(productId, itemId, ct);
        if (item is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.ItemNotFound);
        _uow.Products.RemoveItem(item);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(ApiStatusMessages.Product.ItemDeleted);
    }

    public async Task<IServiceResult<ProductImageResponse>> AddImageAsync(Guid productId, Guid userId, bool isAdmin, CreateProductImageRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductImageResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (string.IsNullOrWhiteSpace(request.Url))
            return ServiceResult<ProductImageResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.ImageUrlRequired);

        var image = new ProductImage { ProductId = productId, Url = request.Url, SortOrder = request.SortOrder };
        await _uow.Products.AddImageAsync(image, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductImageResponse>.Success(_mapper.Map<ProductImageResponse>(image), ApiStatusMessages.Product.ImageCreated, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<ProductImageResponse>> UploadImageAsync(
        Guid productId, Guid userId, bool isAdmin,
        Stream content, string fileName, string contentType, int sortOrder, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductImageResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;

        if (content is null || content.Length == 0)
            return ServiceResult<ProductImageResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.ImageFileRequired);
        if (!ImageUpload.IsAllowed(contentType))
            return ServiceResult<ProductImageResponse>.Failure(ApiStatusCodes.UnprocessableEntity, ApiStatusMessages.Product.ImageTypeInvalid);

        // Mỗi product một thư mục riêng: Product_images/{productId}/{guid}{ext}
        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(contentType);
        var objectPath = $"Product_images/{productId}/{Guid.NewGuid():N}{ext}";

        var stored = await _storage.UploadAsync(objectPath, content, contentType, ct);

        var image = new ProductImage { ProductId = productId, Url = stored.Url, SortOrder = sortOrder };
        await _uow.Products.AddImageAsync(image, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductImageResponse>.Success(_mapper.Map<ProductImageResponse>(image), ApiStatusMessages.Product.ImageCreated, ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> DeleteImageAsync(Guid productId, Guid imageId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<object>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        var image = await _uow.Products.GetImageAsync(productId, imageId, ct);
        if (image is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.ImageNotFound);

        var linkedModel = await _uow.Products.GetModel3DAsync(productId, imageId, ct);
        if (linkedModel is not null)
            return ServiceResult.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Product.Model3DImageHasModel);

        _uow.Products.RemoveImage(image);
        await _uow.SaveChangesAsync(ct);
        // Xoá file trên storage best-effort sau khi DB đã commit (không chặn nghiệp vụ nếu lỗi).
        await _storage.DeleteByUrlAsync(image.Url, ct);
        return ServiceResult.Success(ApiStatusMessages.Product.ImageDeleted);
    }

    public async Task<IServiceResult<ProductDetailResponse>> SetCategoriesAsync(Guid productId, Guid userId, bool isAdmin, SetCategoriesRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductDetailResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (!await _uow.Categories.AllExistAsync(request.CategoryIds, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.CategoriesNotExist);

        await _uow.Products.ReplaceCategoriesAsync(productId, request.CategoryIds, ct);
        await _uow.SaveChangesAsync(ct);
        var detail = await _uow.Products.GetDetailAsync(productId, ct);
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(detail), ApiStatusMessages.Product.CategoriesUpdated);
    }

    public async Task<IServiceResult<ProductFengShuiResponse>> SetFengShuiAsync(Guid productId, Guid userId, bool isAdmin, SetProductFengShuiRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductFengShuiResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;

        // Element là enum (FengShuiElement) nên luôn hợp lệ; chỉ cần kiểm style/vibe code có trong bảng tra cứu
        // → tránh lỗi FK 500, trả 400 thân thiện.
        if (!await AllCodesExistAsync(_uow.Styles, request.Styles, ct))
            return ServiceResult<ProductFengShuiResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.StylesNotExist);
        if (!await AllCodesExistAsync(_uow.Vibes, request.Vibes, ct))
            return ServiceResult<ProductFengShuiResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Product.VibesNotExist);

        var placement = request.Placement ?? ProductPlacement.Desk;
        await _uow.Products.SetFengShuiAsync(
            productId, request.PrimaryElement, request.SecondaryElements, placement, ct);
        await _uow.Products.ReplaceVibesAsync(productId, request.Vibes, ct);
        await _uow.Products.ReplaceStylesAsync(productId, request.Styles, ct);
        // Vendor chỉ ĐỀ XUẤT thẻ mục tiêu; thẻ đã duyệt không bị hạ khi vendor sửa lại danh sách.
        await _uow.Products.ReplaceProposedAspirationsAsync(productId, request.Aspirations, ct);
        await _uow.SaveChangesAsync(ct);

        var aspirations = await _uow.Products.GetAspirationsAsync(productId, ct);

        return ServiceResult<ProductFengShuiResponse>.Success(new ProductFengShuiResponse
        {
            ProductId = productId,
            PrimaryElement = request.PrimaryElement,
            SecondaryElements = request.SecondaryElements.Distinct().Where(e => e != request.PrimaryElement).ToList(),
            Placement = placement,
            ApprovedAspirations = aspirations.Where(a => a.IsApproved).Select(a => a.Aspiration).ToList(),
            PendingAspirations = aspirations.Where(a => !a.IsApproved).Select(a => a.Aspiration).ToList(),
            Vibes = request.Vibes.Distinct().ToList(),
            Styles = request.Styles.Distinct().ToList(),
        }, "Cập nhật thuộc tính phong thủy thành công.");
    }

    /// <summary>
    /// Admin/manager duyệt thẻ mục tiêu. Tách khỏi <see cref="SetFengShuiAsync"/> vì đây là quyền SÀN:
    /// thẻ "Tài lộc" là lời hứa nghiệp vụ, để vendor tự bật thì ai cũng gắn hết để lên top.
    /// </summary>
    public async Task<IServiceResult<ProductFengShuiResponse>> ApproveAspirationsAsync(
        Guid productId, Guid approverId, ApproveProductAspirationsRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetDetailAsync(productId, ct);
        if (product is null)
            return ServiceResult<ProductFengShuiResponse>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound);

        var rows = await _uow.Products.ApproveAspirationsAsync(productId, request.Approved, approverId, ct);
        await _uow.SaveChangesAsync(ct);

        var primary = product.Elements.FirstOrDefault(e => e.IsPrimary);
        return ServiceResult<ProductFengShuiResponse>.Success(new ProductFengShuiResponse
        {
            ProductId = productId,
            PrimaryElement = primary?.Element ?? default,
            SecondaryElements = product.Elements.Where(e => !e.IsPrimary).Select(e => e.Element).ToList(),
            Placement = product.Placement,
            ApprovedAspirations = rows.Where(a => a.IsApproved).Select(a => a.Aspiration).ToList(),
            PendingAspirations = rows.Where(a => !a.IsApproved).Select(a => a.Aspiration).ToList(),
            Vibes = product.Vibes.Select(v => v.VibeCode).ToList(),
            Styles = product.Styles.Select(v => v.StyleCode).ToList(),
        }, "Cập nhật duyệt thẻ mục tiêu thành công.");
    }

    /// <summary>True nếu mọi code (style/vibe...) đều tồn tại trong bảng tra cứu. Tập rỗng → true.</summary>
    private static async Task<bool> AllCodesExistAsync<T>(
        IGenericRepository<T> repo, IEnumerable<string> codes, CancellationToken ct)
        where T : class, Domain.Entities.Catalog.ILookup
    {
        var wanted = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0) return true;
        var existing = (await repo.GetAllAsync(ct)).Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return wanted.All(existing.Contains);
    }

    // ---- helpers ----

    /// <summary>
    /// Vendor bỏ trống → sàn tự sinh mã; vendor tự nhập → kiểm trùng và trả lỗi 400 thân thiện
    /// thay vì để unique index của Postgres ném exception thành 500.
    /// </summary>
    private async Task<(string? Value, string? Error)> ResolveSkuAsync(
        string? requested, Guid? excludeItemId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requested))
            return (await _skuGenerator.GenerateAsync(ct), null);

        var sku = requested.Trim();
        return await _uow.Products.SkuExistsAsync(sku, excludeItemId, ct)
            ? (null, ApiStatusMessages.Product.SkuDuplicated)
            : (sku, null);
    }

    private async Task<bool> CanManageStoreAsync(Guid storeId, Guid userId, bool isAdmin, CancellationToken ct)
        => isAdmin || await _uow.Stores.CanManageAsync(storeId, userId, ct);

    /// <summary>
    /// Gắn thuộc tính phong thủy lên product mới (in-memory). SizeClass/Vibes/Styles độc lập với PrimaryElement
    /// (vendor có thể chọn vibe/style mà không cần khai hành); hành chính/phụ chỉ gắn khi có khai PrimaryElement.
    /// </summary>
    private static void ApplyFengShui(Product product, CreateProductRequest request)
    {
        product.Placement = request.Placement ?? ProductPlacement.Desk;
        if (request.PrimaryElement is { } primary)
        {
            product.Elements.Add(new ProductElement { Element = primary, IsPrimary = true });
            foreach (var el in request.SecondaryElements.Distinct().Where(e => e != primary))
                product.Elements.Add(new ProductElement { Element = el, IsPrimary = false });
        }
        foreach (var code in request.Vibes.Distinct())
            product.Vibes.Add(new ProductVibe { VibeCode = code });
        foreach (var code in request.Styles.Distinct())
            product.Styles.Add(new ProductStyle { StyleCode = code });
    }

    /// <summary>Load product + check quyền quản lý. Trả về Error đã set nếu fail.</summary>
    private async Task<(ServiceResult<T>? Error, Product? Product)> GuardProductAsync<T>(Guid productId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return (ServiceResult<T>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Product.NotFound), null);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return (ServiceResult<T>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Product.ManageForbidden), null);
        return (null, product);
    }
}
