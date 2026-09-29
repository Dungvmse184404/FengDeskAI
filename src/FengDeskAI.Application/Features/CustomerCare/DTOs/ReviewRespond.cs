namespace FengDeskAI.Application.Features.CustomerCare.DTOs
{
    /// <summary>Người viết đánh giá — chỉ thông tin công khai, KHÔNG trả entity User.</summary>
    public class ReviewerResponse
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
    }

    public class ReviewResponse
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid UserId { get; set; }
        public ReviewerResponse? User { get; set; }
        /// <summary>Null khi sản phẩm đã bị xoá cứng — đánh giá vẫn giữ.</summary>
        public Guid? ProductId { get; set; }
        /// <summary>Tên sản phẩm lúc viết đánh giá.</summary>
        public string? ProductName { get; set; }
        public Guid? GardenStoreId { get; set; }
        public Guid? OrderItemId { get; set; }
        /// <summary>Biến thể đã mua (chụp ở dòng đơn).</summary>
        public string? VariantName { get; set; }
    }

    public class CreateReviewRespond
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? ProductId { get; set; }
        public Guid? OrderItemId { get; set; }
    }

    public class UpdateReviewRespond
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>Điểm trung bình + số lượt + phân bố 1–5 sao (<c>Distribution[0]</c> = số lượt 1 sao).</summary>
    public class RatingSummaryResponse
    {
        public double Average { get; set; }
        public int Count { get; set; }
        public int[] Distribution { get; set; } = new int[5];
    }

    /// <summary>Trạng thái đánh giá của một dòng đơn — FE dựa vào đây để hiện nút / ghi chú.</summary>
    public enum ReviewEligibilityStatus
    {
        /// <summary>Đã giao, chưa đánh giá → được đánh giá.</summary>
        Reviewable,
        /// <summary>Đã có đánh giá cho dòng đơn này.</summary>
        Reviewed,
        /// <summary>Đã hoàn hàng (hoàn tiền xong / delivery Returned) → không đánh giá, FE ghi chú "Đã hoàn hàng".</summary>
        Returned,
        /// <summary>Phần hàng của cửa hàng chưa giao xong.</summary>
        NotDelivered,
        /// <summary>Sản phẩm đã bị xoá khỏi sàn.</summary>
        ProductUnavailable,
    }

    public class ReviewableOrderItemResponse
    {
        public Guid OrderItemId { get; set; }
        public Guid OrderId { get; set; }
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? VariantName { get; set; }
        public string? ImageUrl { get; set; }
        public ReviewEligibilityStatus Status { get; set; }
        public Guid? ReviewId { get; set; }
    }

    /// <summary>Trang sản phẩm: user hiện tại có được đánh giá sản phẩm này không, và nếu không thì vì sao.</summary>
    public class ReviewEligibilityResponse
    {
        public bool CanReview { get; set; }
        /// <summary>Null khi user chưa từng mua sản phẩm.</summary>
        public ReviewEligibilityStatus? Status { get; set; }
        /// <summary>Dòng đơn sẽ được đánh giá khi <see cref="CanReview"/>.</summary>
        public Guid? OrderItemId { get; set; }
    }
}
