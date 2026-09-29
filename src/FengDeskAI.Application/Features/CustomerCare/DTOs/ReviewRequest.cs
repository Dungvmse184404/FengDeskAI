using FengDeskAI.Application.Common.Models;

namespace FengDeskAI.Application.Features.CustomerCare.DTOs
{
    /// <summary>
    /// Tạo đánh giá. Ưu tiên <see cref="OrderItemId"/> (đánh giá đúng dòng đơn — trang Đơn hàng). Chỉ gửi
    /// <see cref="ProductId"/> (trang sản phẩm) thì hệ thống chọn dòng đơn mới nhất đủ điều kiện, chưa đánh giá.
    /// </summary>
    public class CreateReviewRequest
    {
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public Guid? OrderItemId { get; set; }
        public Guid? ProductId { get; set; }
    }

    public class UpdateReviewRequest
    {
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
    }

    /// <summary>Lọc danh sách đánh giá công khai — theo sản phẩm hoặc cửa hàng, có phân trang.</summary>
    public class ReviewQueryParams : PageRequest
    {
        public Guid? ProductId { get; set; }
        public Guid? StoreId { get; set; }
    }

    /// <summary>Tóm tắt điểm — đúng MỘT trong hai: sản phẩm hoặc cửa hàng.</summary>
    public class RatingSummaryQuery
    {
        public Guid? ProductId { get; set; }
        public Guid? StoreId { get; set; }
    }
}
