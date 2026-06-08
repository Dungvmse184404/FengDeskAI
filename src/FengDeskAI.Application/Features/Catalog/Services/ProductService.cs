using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Features.Catalog.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ProductService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<PagedResult<ProductListItemResponse>>> SearchAsync(ProductQueryParams query, CancellationToken ct = default)
    {
        var filter = new ProductSearchFilter
        {
            StoreId = query.StoreId,
            CategoryId = query.CategoryId,
            TagId = query.TagId,
            Search = query.Search,
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
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm.");
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(product));
    }

    public async Task<IServiceResult<ProductDetailResponse>> CreateAsync(Guid userId, bool isAdmin, CreateProductRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Tên sản phẩm không được để trống.");
        if (!await CanManageStoreAsync(request.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền tạo sản phẩm cho cửa hàng này.");
        var linkError = await ValidateLinksAsync(request.CategoryIds, request.TagIds, ct);
        if (linkError is not null)
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, linkError);

        var product = new Product
        {
            GardenStoreId = request.GardenStoreId,
            Name = request.Name.Trim(),
            Description = request.Description,
            IsActive = true,
        };
        foreach (var i in request.Items)
            product.Items.Add(new ProductItem { Name = i.Name, Price = i.Price, Stock = i.Stock, Sku = i.Sku });
        foreach (var img in request.Images)
            product.Images.Add(new ProductImage { Url = img.Url, SortOrder = img.SortOrder });
        foreach (var cid in request.CategoryIds.Distinct())
            product.ProductCategories.Add(new ProductCategory { CategoryId = cid });
        foreach (var tid in request.TagIds.Distinct())
            product.ProductTags.Add(new ProductTag { TagId = tid });

        await _uow.Products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);

        var detail = await _uow.Products.GetDetailAsync(product.Id, ct);
        return ServiceResult<ProductDetailResponse>.Success(
            _mapper.Map<ProductDetailResponse>(detail), "Tạo sản phẩm thành công.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<ProductDetailResponse>> UpdateAsync(Guid id, Guid userId, bool isAdmin, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product is null)
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm.");
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền sửa sản phẩm này.");
        if (string.IsNullOrWhiteSpace(request.Name))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Tên sản phẩm không được để trống.");

        product.Name = request.Name.Trim();
        product.Description = request.Description;
        product.IsActive = request.IsActive;
        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        var detail = await _uow.Products.GetDetailAsync(id, ct);
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(detail), "Cập nhật sản phẩm thành công.");
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm.");
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xóa sản phẩm này.");
        _uow.Products.Remove(product);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa sản phẩm.");
    }

    public async Task<IServiceResult<ProductItemResponse>> AddItemAsync(Guid productId, Guid userId, bool isAdmin, CreateProductItemRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductItemResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (request.Price < 0)
            return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.BadRequest, "Giá không hợp lệ.");

        var item = new ProductItem { ProductId = productId, Name = request.Name, Price = request.Price, Stock = request.Stock, Sku = request.Sku };
        await _uow.Products.AddItemAsync(item, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductItemResponse>.Success(_mapper.Map<ProductItemResponse>(item), "Thêm biến thể thành công.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<ProductItemResponse>> UpdateItemAsync(Guid productId, Guid itemId, Guid userId, bool isAdmin, UpdateProductItemRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductItemResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;

        var item = await _uow.Products.GetItemAsync(productId, itemId, ct);
        if (item is null) return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy biến thể.");
        if (request.Price < 0) return ServiceResult<ProductItemResponse>.Failure(ApiStatusCodes.BadRequest, "Giá không hợp lệ.");

        item.Name = request.Name;
        item.Price = request.Price;
        item.Stock = request.Stock;
        item.Sku = request.Sku;
        // item được track bởi DbContext (GetItemAsync không AsNoTracking) → SaveChanges tự persist
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductItemResponse>.Success(_mapper.Map<ProductItemResponse>(item), "Cập nhật biến thể thành công.");
    }

    public async Task<IServiceResult> DeleteItemAsync(Guid productId, Guid itemId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<object>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        var item = await _uow.Products.GetItemAsync(productId, itemId, ct);
        if (item is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy biến thể.");
        _uow.Products.RemoveItem(item);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa biến thể.");
    }

    public async Task<IServiceResult<ProductImageResponse>> AddImageAsync(Guid productId, Guid userId, bool isAdmin, CreateProductImageRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductImageResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (string.IsNullOrWhiteSpace(request.Url))
            return ServiceResult<ProductImageResponse>.Failure(ApiStatusCodes.BadRequest, "URL ảnh không được để trống.");

        var image = new ProductImage { ProductId = productId, Url = request.Url, SortOrder = request.SortOrder };
        await _uow.Products.AddImageAsync(image, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ProductImageResponse>.Success(_mapper.Map<ProductImageResponse>(image), "Thêm ảnh thành công.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> DeleteImageAsync(Guid productId, Guid imageId, Guid userId, bool isAdmin, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<object>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        var image = await _uow.Products.GetImageAsync(productId, imageId, ct);
        if (image is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy ảnh.");
        _uow.Products.RemoveImage(image);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa ảnh.");
    }

    public async Task<IServiceResult<ProductDetailResponse>> SetCategoriesAsync(Guid productId, Guid userId, bool isAdmin, SetCategoriesRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductDetailResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (!await _uow.Categories.AllExistAsync(request.CategoryIds, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Có danh mục không tồn tại.");

        await _uow.Products.ReplaceCategoriesAsync(productId, request.CategoryIds, ct);
        await _uow.SaveChangesAsync(ct);
        var detail = await _uow.Products.GetDetailAsync(productId, ct);
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(detail), "Cập nhật danh mục sản phẩm thành công.");
    }

    public async Task<IServiceResult<ProductDetailResponse>> SetTagsAsync(Guid productId, Guid userId, bool isAdmin, SetTagsRequest request, CancellationToken ct = default)
    {
        var guard = await GuardProductAsync<ProductDetailResponse>(productId, userId, isAdmin, ct);
        if (guard.Error is not null) return guard.Error;
        if (!await _uow.Tags.AllExistAsync(request.TagIds, ct))
            return ServiceResult<ProductDetailResponse>.Failure(ApiStatusCodes.BadRequest, "Có tag không tồn tại.");

        await _uow.Products.ReplaceTagsAsync(productId, request.TagIds, ct);
        await _uow.SaveChangesAsync(ct);
        var detail = await _uow.Products.GetDetailAsync(productId, ct);
        return ServiceResult<ProductDetailResponse>.Success(_mapper.Map<ProductDetailResponse>(detail), "Cập nhật tag sản phẩm thành công.");
    }

    // ---- helpers ----

    private async Task<bool> CanManageStoreAsync(Guid storeId, Guid userId, bool isAdmin, CancellationToken ct)
        => isAdmin || await _uow.Stores.CanManageAsync(storeId, userId, ct);

    private async Task<string?> ValidateLinksAsync(IEnumerable<Guid> categoryIds, IEnumerable<Guid> tagIds, CancellationToken ct)
    {
        if (!await _uow.Categories.AllExistAsync(categoryIds, ct)) return "Có danh mục không tồn tại.";
        if (!await _uow.Tags.AllExistAsync(tagIds, ct)) return "Có tag không tồn tại.";
        return null;
    }

    /// <summary>Load product + check quyền quản lý. Trả về Error đã set nếu fail.</summary>
    private async Task<(ServiceResult<T>? Error, Product? Product)> GuardProductAsync<T>(Guid productId, Guid userId, bool isAdmin, CancellationToken ct)
    {
        var product = await _uow.Products.GetByIdAsync(productId, ct);
        if (product is null)
            return (ServiceResult<T>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy sản phẩm."), null);
        if (!await CanManageStoreAsync(product.GardenStoreId, userId, isAdmin, ct))
            return (ServiceResult<T>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền thao tác sản phẩm này."), null);
        return (null, product);
    }
}
