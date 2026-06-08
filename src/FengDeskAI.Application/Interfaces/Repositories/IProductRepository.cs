using FengDeskAI.Domain.Entities.Catalog;

namespace FengDeskAI.Application.Interfaces.Repositories;

public class ProductSearchFilter
{
    public Guid? StoreId { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? TagId { get; set; }
    public string? Search { get; set; }
    public bool ActiveOnly { get; set; } = true;
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
}

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Product kèm Items/Images/Categories/Tags/Store — dùng cho trang chi tiết.</summary>
    Task<Product?> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load product (tracked) kèm child collections để cập nhật quan hệ.</summary>
    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<(List<Product> Items, int Total)> SearchAsync(ProductSearchFilter filter, CancellationToken ct = default);

    // Quản lý product item (SKU) — sub-resource
    Task<ProductItem?> GetItemAsync(Guid productId, Guid itemId, CancellationToken ct = default);
    Task AddItemAsync(ProductItem item, CancellationToken ct = default);
    void RemoveItem(ProductItem item);

    // Quản lý ảnh
    Task<ProductImage?> GetImageAsync(Guid productId, Guid imageId, CancellationToken ct = default);
    Task AddImageAsync(ProductImage image, CancellationToken ct = default);
    void RemoveImage(ProductImage image);

    // Thay thế toàn bộ liên kết category/tag của product
    Task ReplaceCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds, CancellationToken ct = default);
    Task ReplaceTagsAsync(Guid productId, IEnumerable<Guid> tagIds, CancellationToken ct = default);
}
