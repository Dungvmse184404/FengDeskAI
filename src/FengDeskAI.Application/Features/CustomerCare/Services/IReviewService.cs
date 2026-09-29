using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;

namespace FengDeskAI.Application.Features.CustomerCare.Services
{
    public interface IReviewService
    {
        Task<IServiceResult<PagedResult<ReviewResponse>>> GetListAsync(ReviewQueryParams query, CancellationToken ct = default);
        Task<IServiceResult<RatingSummaryResponse>> GetSummaryAsync(RatingSummaryQuery query, CancellationToken ct = default);
        Task<IServiceResult<List<ReviewResponse>>> GetMyAsync(Guid userId, CancellationToken ct = default);

        /// <summary>Trạng thái đánh giá từng dòng của một đơn (của chính user) — trang Đơn hàng.</summary>
        Task<IServiceResult<List<ReviewableOrderItemResponse>>> GetOrderItemsAsync(Guid userId, Guid orderId, CancellationToken ct = default);

        /// <summary>User có được đánh giá sản phẩm này không — trang sản phẩm.</summary>
        Task<IServiceResult<ReviewEligibilityResponse>> GetEligibilityAsync(Guid userId, Guid productId, CancellationToken ct = default);

        Task<IServiceResult<CreateReviewRespond>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default);
        Task<IServiceResult<UpdateReviewRespond>> UpdateAsync(Guid id, Guid userId, UpdateReviewRequest request, CancellationToken ct = default);
        Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    }
}
