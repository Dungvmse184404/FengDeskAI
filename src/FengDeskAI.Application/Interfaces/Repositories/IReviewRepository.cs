using FengDeskAI.Domain.Entities.CustomerCare;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IReviewRepository : IGenericRepository<Review>
{
    Task<List<Review>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<Review>> GetByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<Review?> GetByIdWithUserAsync(Guid id, CancellationToken ct = default);

    /// <summary>Kiểm tra user đã mua sản phẩm (có order Paid/Completed chứa ProductItem thuộc Product).</summary>
    Task<bool> HasUserPurchasedProductAsync(Guid userId, Guid productId, CancellationToken ct = default);

    /// <summary>Kiểm tra user đã review sản phẩm này chưa (tránh trùng).</summary>
    Task<bool> HasUserReviewedProductAsync(Guid userId, Guid productId, CancellationToken ct = default);

    /// <summary>Đánh giá trung bình của 1 shop = trung bình Rating của mọi review thuộc các sản phẩm của shop đó
    /// (chưa có bảng đánh giá riêng cho Store). Count=0 khi shop chưa có review nào.</summary>
    Task<(double Average, int Count)> GetStoreRatingSummaryAsync(Guid storeId, CancellationToken ct = default);
}
