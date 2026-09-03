using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Enums.Catalog;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Interfaces.Repositories;

public class ProductSearchFilter
{
    public Guid? StoreId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Search { get; set; }

    /// <summary>Lọc theo hành phong thủy — khớp cả hành chính lẫn hành phụ.</summary>
    public FengShuiElement? Element { get; set; }

    /// <summary>
    /// Lọc theo MỤC TIÊU phong thủy (Tài lộc / Sức khỏe…). Chỉ tính thẻ ĐÃ DUYỆT
    /// (<c>product_aspirations.is_approved</c>).
    /// </summary>
    public Aspiration? Aspiration { get; set; }

    /// <summary>
    /// True → chỉ lấy sản phẩm có model 3D xem được (Succeeded + có ModelUrl + owner chưa tắt hiển thị).
    /// Null/false = không lọc.
    /// </summary>
    public bool? HasModel3D { get; set; }
    public bool ActiveOnly { get; set; } = true;
    public int Skip { get; set; }
    public int Take { get; set; } = 20;
}

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>Product kèm Items/Images/Categories/Elements/Vibes/Styles/Store — dùng cho trang chi tiết.</summary>
    Task<Product?> GetDetailAsync(Guid id, CancellationToken ct = default);

    /// <summary>Load product (tracked) kèm child collections để cập nhật quan hệ.</summary>
    Task<Product?> GetForUpdateAsync(Guid id, CancellationToken ct = default);

    Task<(List<Product> Items, int Total)> SearchAsync(ProductSearchFilter filter, CancellationToken ct = default);

    /// <summary>
    /// Sản phẩm active đã khai báo thuộc tính phong thủy (<c>FengShui != null</c>) — ứng viên cho engine gợi ý.
    /// Kèm FengShui/Vibes/Styles + Images/Items để chấm điểm và hiển thị.
    /// <paramref name="placements"/> lọc theo vị trí sử dụng (workspace vs mang theo người); null/rỗng = không lọc.
    /// <paramref name="aspiration"/> lọc theo mục tiêu phong thủy user nêu — chỉ tính thẻ ĐÃ DUYỆT
    /// (<c>product_aspirations.is_approved</c>); null = không lọc.
    /// </summary>
    Task<List<Product>> GetScorableCandidatesAsync(
        IReadOnlyCollection<ProductPlacement>? placements = null,
        Aspiration? aspiration = null,
        CancellationToken ct = default);

    // Quản lý product item (SKU) — sub-resource

    /// <summary>
    /// Mã SKU đã được dùng chưa (bỏ qua bản đã soft-delete, khớp filter của unique index).
    /// <paramref name="excludeItemId"/> để loại chính biến thể đang sửa khỏi phép kiểm.
    /// </summary>
    Task<bool> SkuExistsAsync(string sku, Guid? excludeItemId, CancellationToken ct = default);

    Task<ProductItem?> GetItemAsync(Guid productId, Guid itemId, CancellationToken ct = default);
    Task AddItemAsync(ProductItem item, CancellationToken ct = default);
    void RemoveItem(ProductItem item);

    // Quản lý ảnh
    Task<ProductImage?> GetImageAsync(Guid productId, Guid imageId, CancellationToken ct = default);
    Task<List<ProductImage>> ListImagesAsync(Guid productId, CancellationToken ct = default);
    Task AddImageAsync(ProductImage image, CancellationToken ct = default);
    void RemoveImage(ProductImage image);

    // Model 3D theo từng ảnh sản phẩm
    Task<List<ProductModel3D>> ListModel3DsAsync(Guid productId, CancellationToken ct = default);
    Task<ProductModel3D?> GetModel3DAsync(Guid productId, Guid productImageId, CancellationToken ct = default);
    Task<ProductModel3D?> GetModel3DByIdAsync(Guid productId, Guid modelId, CancellationToken ct = default);

    /// <summary>Tìm model 3D của product KỂ CẢ bản đã soft-delete (bỏ query filter) — dùng khi sinh lại
    /// để hồi sinh & tái sử dụng row cũ, tránh đụng unique index <c>product_id</c>.</summary>
    Task<ProductModel3D?> GetModel3DIncludingDeletedAsync(
        Guid productId, Guid productImageId, CancellationToken ct = default);

    Task AddModel3DAsync(ProductModel3D model, CancellationToken ct = default);
    void RemoveModel3D(ProductModel3D model);

    /// <summary>Các model đang Processing — worker nền poll Meshy để hoàn tất. Tracked để cập nhật.</summary>
    Task<List<ProductModel3D>> GetProcessingModel3DsAsync(CancellationToken ct = default);

    // ----- Model3DRequest (hàng chờ + lịch sử, n–1 với product) -----

    /// <summary>Request đang "mở" (chưa Succeeded/Failed/Rejected) của product — dùng để chặn tạo chồng request.</summary>
    Task<Model3DRequest?> GetOpenModel3DRequestAsync(
        Guid productId, Guid productImageId, CancellationToken ct = default);

    Task AddModel3DRequestAsync(Model3DRequest request, CancellationToken ct = default);

    /// <summary>Load 1 request theo id (tracked) — dùng cho các thao tác staff sàn (generate/retry/accept/reject).</summary>
    Task<Model3DRequest?> GetModel3DRequestAsync(Guid requestId, CancellationToken ct = default);

    /// <summary>Lịch sử request của 1 product, mới nhất trước.</summary>
    Task<List<Model3DRequest>> ListModel3DRequestsAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Hàng chờ cho staff sàn — kèm <c>Product</c>/<c>Product.Store</c> để hiển thị. Lọc theo
    /// <paramref name="status"/> và/hoặc <paramref name="reason"/> (vd Queued + InsufficientCredits
    /// = các request Initial đang kẹt vì hết credit Meshy).
    /// </summary>
    Task<(List<Model3DRequest> Items, int Total, Dictionary<Model3DRequestStatus, int> StatusCounts)> GetStaffQueueAsync(
        Model3DRequestStatus? status, Model3DFailureReason? reason,
        int skip, int take, CancellationToken ct = default);

    /// <summary>Request Initial đang Queued và đến hạn thử lại (NextAttemptAt null hoặc &lt;= now) — cho worker.</summary>
    Task<List<Model3DRequest>> GetDueInitialQueueAsync(DateTime now, CancellationToken ct = default);

    /// <summary>Request Initial đang Processing (đã gửi Meshy) — worker poll kết quả.</summary>
    Task<List<Model3DRequest>> GetProcessingInitialRequestsAsync(CancellationToken ct = default);

    // Thay thế toàn bộ liên kết category của product
    Task ReplaceCategoriesAsync(Guid productId, IEnumerable<Guid> categoryIds, CancellationToken ct = default);

    // Thuộc tính phong thủy (ứng viên gợi ý): set hành chính + các hành phụ (product_element) + placement (trên products).
    // Kích thước KHÔNG nằm ở đây — nó thuộc từng biến thể (product_items.size_class).
    Task SetFengShuiAsync(Guid productId, FengShuiElement primary, IEnumerable<FengShuiElement> secondaries, ProductPlacement placement, CancellationToken ct = default);

    /// <summary>
    /// Ghi lại danh sách thẻ mục tiêu vendor ĐỀ XUẤT. Thẻ đã được admin duyệt KHÔNG bị đụng tới —
    /// vendor sửa đề xuất không được phép tự hạ thẻ đã duyệt của mình.
    /// </summary>
    Task ReplaceProposedAspirationsAsync(Guid productId, IEnumerable<Aspiration> aspirations, CancellationToken ct = default);

    /// <summary>
    /// Admin duyệt: thẻ trong <paramref name="approved"/> được bật (tạo mới nếu chưa có),
    /// thẻ còn lại của sản phẩm bị hạ về chưa duyệt. Trả danh sách thẻ hiện có sau khi cập nhật.
    /// </summary>
    Task<List<ProductAspiration>> ApproveAspirationsAsync(
        Guid productId, IEnumerable<Aspiration> approved, Guid approvedBy, CancellationToken ct = default);

    /// <summary>Thẻ mục tiêu của một sản phẩm (cả đã duyệt lẫn chưa).</summary>
    Task<List<ProductAspiration>> GetAspirationsAsync(Guid productId, CancellationToken ct = default);
    Task ReplaceVibesAsync(Guid productId, IEnumerable<string> vibeCodes, CancellationToken ct = default);
    Task ReplaceStylesAsync(Guid productId, IEnumerable<string> styleCodes, CancellationToken ct = default);
}
