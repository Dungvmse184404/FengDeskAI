using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Catalog.DTOs;

namespace FengDeskAI.Application.Features.Catalog.Services;

public interface IProductService
{
    Task<IServiceResult<PagedResult<ProductListItemResponse>>> SearchAsync(ProductQueryParams query, CancellationToken ct = default);
    Task<IServiceResult<ProductDetailResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IServiceResult<ProductDetailResponse>> CreateAsync(Guid userId, bool isAdmin, CreateProductRequest request, CancellationToken ct = default);
    Task<IServiceResult<ProductDetailResponse>> UpdateAsync(Guid id, Guid userId, bool isAdmin, UpdateProductRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeleteAsync(Guid id, Guid userId, bool isAdmin, CancellationToken ct = default);

    Task<IServiceResult<ProductItemResponse>> AddItemAsync(Guid productId, Guid userId, bool isAdmin, CreateProductItemRequest request, CancellationToken ct = default);
    Task<IServiceResult<ProductItemResponse>> UpdateItemAsync(Guid productId, Guid itemId, Guid userId, bool isAdmin, UpdateProductItemRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeleteItemAsync(Guid productId, Guid itemId, Guid userId, bool isAdmin, CancellationToken ct = default);

    Task<IServiceResult<ProductImageResponse>> AddImageAsync(Guid productId, Guid userId, bool isAdmin, CreateProductImageRequest request, CancellationToken ct = default);

    /// <summary>Tải tệp ảnh lên storage (Product_images/{productId}/...) rồi lưu URL vào product_images.</summary>
    Task<IServiceResult<ProductImageResponse>> UploadImageAsync(
        Guid productId, Guid userId, bool isAdmin,
        Stream content, string fileName, string contentType, int sortOrder, CancellationToken ct = default);

    Task<IServiceResult> DeleteImageAsync(Guid productId, Guid imageId, Guid userId, bool isAdmin, CancellationToken ct = default);

    Task<IServiceResult<ProductDetailResponse>> SetCategoriesAsync(Guid productId, Guid userId, bool isAdmin, SetCategoriesRequest request, CancellationToken ct = default);

    /// <summary>Khai báo/cập nhật thuộc tính phong thủy cho sản phẩm (làm ứng viên gợi ý).</summary>
    Task<IServiceResult<ProductFengShuiResponse>> SetFengShuiAsync(Guid productId, Guid userId, bool isAdmin, SetProductFengShuiRequest request, CancellationToken ct = default);
}
