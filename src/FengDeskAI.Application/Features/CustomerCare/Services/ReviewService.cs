using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

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

    public async Task<IServiceResult<List<ReviewResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var reviews = await _uow.Reviews.GetAllAsync(ct);
        return ServiceResult<List<ReviewResponse>>.Success(
            _mapper.Map<List<ReviewResponse>>(reviews));
    }

    public async Task<IServiceResult<List<ReviewResponse>>> GetMyAsync(Guid userId, CancellationToken ct = default)
    {
        var reviews = await _uow.Reviews.GetByUserIdAsync(userId, ct);
        return ServiceResult<List<ReviewResponse>>.Success(
            _mapper.Map<List<ReviewResponse>>(reviews));
    }

    public async Task<IServiceResult<CreateReviewRespond>> CreateAsync(Guid userId, CreateReviewRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.ContentRequired);

        if (request.Rating < 1 || request.Rating > 5)
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.RatingInvalid);

        var product = await _uow.Products.GetByIdAsync(request.ProductId, ct);
        if (product is null)
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.NotFound, ApiStatusMessages.Review.ProductNotFound);

        var hasPurchased = await _uow.Reviews.HasUserPurchasedProductAsync(userId, request.ProductId, ct);
        if (!hasPurchased)
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.NotPurchased);

        // Kiểm tra đã review sản phẩm này chưa
        var hasReviewed = await _uow.Reviews.HasUserReviewedProductAsync(userId, request.ProductId, ct);
        if (hasReviewed)
            return ServiceResult<CreateReviewRespond>.Failure(ApiStatusCodes.Conflict, ApiStatusMessages.Review.AlreadyReviewed);

        // Tạo review
        var entity = _mapper.Map<Review>(request);
        entity.UserId = userId;

        await _uow.Reviews.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Review created: {ReviewId} by user {UserId} for product {ProductId}",
            entity.Id, userId, request.ProductId);

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

        // Kiểm tra review thuộc về user
        if (review.UserId != userId)
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.Unauthorized);

        // Validate input
        if (string.IsNullOrWhiteSpace(request.Content))
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.ContentRequired);

        if (request.Rating < 1 || request.Rating > 5)
            return ServiceResult<UpdateReviewRespond>.Failure(ApiStatusCodes.BadRequest, ApiStatusMessages.Review.RatingInvalid);

        // Cập nhật
        review.Content = request.Content;
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

        // Kiểm tra review thuộc về user
        if (review.UserId != userId)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, ApiStatusMessages.Review.Unauthorized);

        _uow.Reviews.Remove(review);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Review deleted: {ReviewId} by user {UserId}", id, userId);

        return ServiceResult.Success(ApiStatusMessages.Review.Deleted);
    }
}
