using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Catalog;
using FengDeskAI.Domain.Entities.Geography;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Entities.Sales;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FengDeskAI.Domain.Entities.CustomerCare
{
    public class Review : BaseEntity
    {
        public string Content { get; set; } = string.Empty;
        public int Rating { get; set; }
        public Guid UserId { get; set; }
        public virtual User User { get; set; } = null!;
        /// <summary>Null khi sản phẩm bị Manager xoá cứng — đánh giá vẫn giữ, hiển thị "sản phẩm không còn bán".</summary>
        public Guid? ProductId { get; set; }
        public virtual Product? Product { get; set; }

        /// <summary>Chụp lúc viết đánh giá — còn nguyên khi sản phẩm đã bị xoá.</summary>
        public string? ProductName { get; set; }
        /// <summary>Cửa hàng của sản phẩm — điểm đánh giá cửa hàng tính từ đây, không qua sản phẩm.</summary>
        public Guid? GardenStoreId { get; set; }

        /// <summary>
        /// Dòng đơn hàng được đánh giá — mỗi dòng đơn chỉ một đánh giá (mua lại thì đánh giá lần nữa).
        /// Null với đánh giá cũ không truy được về đơn.
        /// </summary>
        public Guid? OrderItemId { get; set; }
        public virtual OrderItem? OrderItem { get; set; }

    }
}
