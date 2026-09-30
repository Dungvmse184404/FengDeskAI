using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Enums.Sales;
using Xunit;

namespace FengDeskAI.UnitTests;

/// <summary>Luật được đánh giá theo dòng đơn — xem <see cref="ReviewService.Evaluate"/>.</summary>
public class ReviewEligibilityTests
{
    private static ReviewCandidateRow Row(
        DeliveryStatus? delivery = DeliveryStatus.Delivered,
        bool refunded = false,
        bool productExists = true,
        Guid? reviewId = null)
        => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Cây kim tiền", "Chậu nhỏ", null, Guid.NewGuid(),
            delivery, refunded, productExists, reviewId, DateTime.UtcNow);

    [Fact]
    public void Evaluate_DeliveredAndNotReviewed_IsReviewable()
        => Assert.Equal(ReviewEligibilityStatus.Reviewable, ReviewService.Evaluate(Row()));

    [Theory]
    [InlineData(null)]
    [InlineData(DeliveryStatus.Pending)]
    [InlineData(DeliveryStatus.Shipped)]
    [InlineData(DeliveryStatus.DeliveryFailed)]
    [InlineData(DeliveryStatus.Cancelled)]
    public void Evaluate_NotDelivered_IsBlocked(DeliveryStatus? delivery)
        => Assert.Equal(ReviewEligibilityStatus.NotDelivered, ReviewService.Evaluate(Row(delivery)));

    [Fact]
    public void Evaluate_Refunded_IsReturned()
        => Assert.Equal(ReviewEligibilityStatus.Returned, ReviewService.Evaluate(Row(refunded: true)));

    [Fact]
    public void Evaluate_DeliveryReturned_IsReturned()
        => Assert.Equal(ReviewEligibilityStatus.Returned, ReviewService.Evaluate(Row(DeliveryStatus.Returned)));

    [Fact]
    public void Evaluate_AlreadyReviewed_WinsOverRefund()
        => Assert.Equal(ReviewEligibilityStatus.Reviewed,
            ReviewService.Evaluate(Row(refunded: true, reviewId: Guid.NewGuid())));

    [Fact]
    public void Evaluate_ProductDeleted_IsUnavailable()
        => Assert.Equal(ReviewEligibilityStatus.ProductUnavailable, ReviewService.Evaluate(Row(productExists: false)));

    [Fact]
    public void BlockingStatus_NeverPurchased_IsNull()
        => Assert.Null(ReviewService.BlockingStatus([]));

    [Fact]
    public void BlockingStatus_PrefersNotDeliveredOverReturned()
        => Assert.Equal(ReviewEligibilityStatus.NotDelivered,
            ReviewService.BlockingStatus([Row(refunded: true), Row(DeliveryStatus.Shipped)]));

    [Fact]
    public void ToSummary_ComputesAverageAndCount()
    {
        var summary = ReviewService.ToSummary([0, 0, 1, 1, 1]);

        Assert.Equal(3, summary.Count);
        Assert.Equal(4.0, summary.Average);
    }

    [Fact]
    public void ToSummary_NoReviews_IsZero()
    {
        var summary = ReviewService.ToSummary(new int[5]);

        Assert.Equal(0, summary.Count);
        Assert.Equal(0, summary.Average);
    }
}
