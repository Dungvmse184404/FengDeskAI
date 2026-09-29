using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>
/// Một dòng đơn của khách kèm dữ kiện để xét quyền đánh giá. <c>IsRefunded</c> = có yêu cầu trả hàng loại hoàn
/// tiền đã hoàn tất trên dòng này; <c>ProductExists</c> = sản phẩm còn trên sàn (chưa xoá).
/// </summary>
public sealed record ReviewCandidateRow(
    Guid OrderItemId,
    Guid OrderId,
    Guid? ProductId,
    string ProductName,
    string? VariantName,
    string? ImageUrl,
    Guid? GardenStoreId,
    DeliveryStatus? DeliveryStatus,
    bool IsRefunded,
    bool ProductExists,
    Guid? ReviewId,
    DateTime OrderedAt);

public interface IReviewRepository : IGenericRepository<Review>
{
    Task<List<Review>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đánh giá công khai (kèm người viết), mới nhất trước. Lọc theo sản phẩm và/hoặc cửa hàng.</summary>
    Task<(List<Review> Items, int TotalCount)> GetPagedAsync(
        Guid? productId, Guid? storeId, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Các dòng đơn của khách làm ứng viên đánh giá, dòng mới đặt trước. Lọc theo đơn / dòng đơn / sản phẩm
    /// (null = không lọc theo tiêu chí đó).
    /// </summary>
    Task<List<ReviewCandidateRow>> GetCandidatesAsync(
        Guid userId, Guid? orderId = null, Guid? orderItemId = null, Guid? productId = null, CancellationToken ct = default);

    /// <summary>Số lượt theo từng mức sao (index 0 = 1 sao) — MỘT lượt đi về DB.</summary>
    Task<int[]> GetRatingDistributionAsync(Guid? productId, Guid? storeId, CancellationToken ct = default);

    /// <summary>Đánh giá trung bình của 1 shop (theo cột chụp <c>garden_store_id</c>). Count=0 khi chưa có review.</summary>
    Task<(double Average, int Count)> GetStoreRatingSummaryAsync(Guid storeId, CancellationToken ct = default);
}
