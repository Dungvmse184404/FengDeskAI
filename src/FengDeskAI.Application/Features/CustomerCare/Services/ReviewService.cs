using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.Sales;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

/// <summary>
/// Đánh giá gắn với DÒNG ĐƠN: chỉ đánh giá khi phần hàng của cửa hàng đã giao (Delivered) và dòng đó chưa bị hoàn
/// tiền; mỗi dòng đơn một đánh giá. Điểm cửa hàng = trung bình mọi đánh giá có <c>garden_store_id</c> của cửa hàng.
/// </summary>
public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(IUnitOfWork uow, IMapper mapper, ILogger<ReviewService> logger)
    {
        _uow = uow;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<IServiceResult<PagedResult<ReviewResponse>>> GetListAsync(ReviewQueryParams query, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Reviews.GetPagedAsync(query.ProductId, query.StoreId, query.Skip, query.PageSize, ct);
        return ServiceResult<PagedResult<ReviewResponse>>.Success(new PagedResult<ReviewResponse>(
            _mapper.Map<List<ReviewResponse>>(items), query.Page, query.PageSize, total));
    }

    public async Task<IServiceResult<RatingSummaryResponse>> GetSummaryAsync(RatingSummaryQuery query, CancellationToken ct = default)
    {
        if (query.ProductId.HasValue == query.StoreId.HasValue)
            return ServiceResult<RatingSummaryResponse>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.SummaryTargetInvalid);

        var distribution = await _uow.Reviews.GetRatingDistributionAsync(query.ProductId, query.StoreId, ct);
        return ServiceResult<RatingSummaryResponse>.Success(ToSummary(distribution));
    }

    public async Task<IServiceResult<List<ReviewResponse>>> GetMyAsync(Guid userId, CancellationToken ct = default)
    {
        var reviews = await _uow.Reviews.GetByUserIdAsync(userId, ct);
        return ServiceResult<List<ReviewResponse>>.Success(_mapper.Map<List<ReviewResponse>>(reviews));
    }

    public async Task<IServiceResult<List<ReviewableOrderItemResponse>>> GetOrderItemsAsync(
        Guid userId, Guid orderId, CancellationToken ct = default)
    {
        var candidates = await _uow.Reviews.GetCandidatesAsync(userId, orderId: orderId, ct: ct);
        return ServiceResult<List<ReviewableOrderItemResponse>>.Success(candidates.Select(c => new ReviewableOrderItemResponse
        {
            OrderItemId = c.OrderItemId,
            OrderId = c.OrderId,
            ProductId = c.ProductId,
            ProductName = c.ProductName,
            VariantName = c.VariantName,
            ImageUrl = c.ImageUrl,
            Status = Evaluate(c),
            ReviewId = c.ReviewId,
        }).ToList());
    }

    public async Task<IServiceResult<ReviewEligibilityResponse>> GetEligibilityAsync(
        Guid userId, Guid productId, CancellationToken ct = default)
    {
        var candidates = await _uow.Reviews.GetCandidatesAsync(userId, productId: productId, ct: ct);
        var reviewable = candidates.FirstOrDefault(c => Evaluate(c) == ReviewEligibilityStatus.Reviewable);

        return ServiceResult<ReviewEligibilityResponse>.Success(reviewable is not null
            ? new ReviewEligibilityResponse
            {
                CanReview = true,
                Status = ReviewEligibilityStatus.Reviewable,
                OrderItemId = reviewable.OrderItemId,
            }
            : new ReviewEligibilityResponse { CanReview = false, Status = BlockingStatus(candidates) });
    }

    public async Task<IServiceResult<CreateReviewRespond>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.ContentRequired);

        if (request.Rating < 1 || request.Rating > 5)
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.RatingInvalid);

        ReviewCandidateRow? target;
        if (request.OrderItemId is { } orderItemId)
        {
            var candidates = await _uow.Reviews.GetCandidatesAsync(userId, orderItemId: orderItemId, ct: ct);
            target = candidates.FirstOrDefault();
            if (target is null)
                return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Review.OrderItemNotFound);

            var status = Evaluate(target);
            if (status != ReviewEligibilityStatus.Reviewable)
                return Rejected<CreateReviewRespond>(status);
        }
        else if (request.ProductId is { } productId)
        {
            // Trang sản phẩm chỉ biết productId → chọn dòng đơn mới nhất còn đánh giá được.
            if (await _uow.Products.GetByIdAsync(productId, ct) is null)
                return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Review.ProductNotFound);

            var candidates = await _uow.Reviews.GetCandidatesAsync(userId, productId: productId, ct: ct);
            target = candidates.FirstOrDefault(c => Evaluate(c) == ReviewEligibilityStatus.Reviewable);
            if (target is null)
                return Rejected<CreateReviewRespond>(BlockingStatus(candidates));
        }
        else
        {
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.TargetRequired);
        }

        var entity = new Review
        {
            UserId = userId,
            Content = request.Content.Trim(),
            Rating = request.Rating,
            OrderItemId = target.OrderItemId,
            ProductId = target.ProductId,
            // Chụp tên + vườn: đánh giá phải còn đọc được khi sản phẩm bị xoá cứng.
            ProductName = target.ProductName,
            GardenStoreId = target.GardenStoreId,
        };

        await _uow.Reviews.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Review created: {ReviewId} by user {UserId} for order item {OrderItemId}",
            entity.Id, userId, target.OrderItemId);

        return ServiceResult<CreateReviewRespond>.Success(
            _mapper.Map<CreateReviewRespond>(entity),
            ApiStatusMessages.Review.Created,
            ApiStatusCodes.Created);
    }

    public async Task<IServiceResult<UpdateReviewRespond>> UpdateAsync(Guid id, Guid userId, UpdateReviewRequest request, CancellationToken ct = default)
    {
        var review = await _uow.Reviews.GetByIdAsync(id, ct);
        if (review is null)
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Review.NotFound);

        if (review.UserId != userId)
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.Unauthorized);

        if (string.IsNullOrWhiteSpace(request.Content))
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.ContentRequired);

        if (request.Rating < 1 || request.Rating > 5)
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.RatingInvalid);

        review.Content = request.Content.Trim();
        review.Rating = request.Rating;
        review.UpdatedAt = DateTime.UtcNow;

        _uow.Reviews.Update(review);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Review updated: {ReviewId} by user {UserId}", id, userId);

        return ServiceResult<UpdateReviewRespond>.Success(
            _mapper.Map<UpdateReviewRespond>(review),
            ApiStatusMessages.Review.Updated);
    }

    public async Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var review = await _uow.Reviews.GetByIdAsync(id, ct);
        if (review is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Review.NotFound);

        if (review.UserId != userId)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.Unauthorized);

        _uow.Reviews.Remove(review);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Review deleted: {ReviewId} by user {UserId}", id, userId);

        return ServiceResult.Success(ApiStatusMessages.Review.Deleted);
    }

    // Thứ tự ưu tiên: đã đánh giá → hoàn hàng → chưa giao → sản phẩm đã xoá → được đánh giá.
    public static ReviewEligibilityStatus Evaluate(ReviewCandidateRow c)
    {
        if (c.ReviewId is not null) return ReviewEligibilityStatus.Reviewed;
        if (c.IsRefunded || c.DeliveryStatus == DeliveryStatus.Returned) return ReviewEligibilityStatus.Returned;
        if (c.DeliveryStatus != DeliveryStatus.Delivered) return ReviewEligibilityStatus.NotDelivered;
        if (!c.ProductExists || c.ProductId is null) return ReviewEligibilityStatus.ProductUnavailable;
        return ReviewEligibilityStatus.Reviewable;
    }

    /// <summary>
    /// Lý do không đánh giá được khi mọi dòng đều bị chặn — ưu tiên lý do "còn hy vọng" (chưa giao) rồi mới tới
    /// đã đánh giá / đã hoàn. Null khi chưa từng mua.
    /// </summary>
    public static ReviewEligibilityStatus? BlockingStatus(IReadOnlyCollection<ReviewCandidateRow> candidates)
    {
        if (candidates.Count == 0) return null;
        var statuses = candidates.Select(Evaluate).ToHashSet();
        foreach (var status in new[]
                 {
                     ReviewEligibilityStatus.NotDelivered, ReviewEligibilityStatus.Reviewed,
                     ReviewEligibilityStatus.Returned, ReviewEligibilityStatus.ProductUnavailable,
                 })
        {
            if (statuses.Contains(status)) return status;
        }
        return null;
    }

    private static IServiceResult<T> Rejected<T>(ReviewEligibilityStatus? status) => status switch
    {
        ReviewEligibilityStatus.Reviewed => ServiceResult<T>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Review.AlreadyReviewed),
        ReviewEligibilityStatus.Returned => ServiceResult<T>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.Returned),
        ReviewEligibilityStatus.NotDelivered => ServiceResult<T>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.NotDelivered),
        ReviewEligibilityStatus.ProductUnavailable => ServiceResult<T>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.ProductUnavailable),
        _ => ServiceResult<T>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.NotPurchased),
    };

    public static RatingSummaryResponse ToSummary(int[] distribution)
    {
        var count = distribution.Sum();
        var average = count == 0 ? 0 : distribution.Select((n, i) => n * (i + 1)).Sum() / (double)count;
        return new RatingSummaryResponse
        {
            Average = Math.Round(average, 1),
            Count = count,
            Distribution = distribution,
        };
    }
}
