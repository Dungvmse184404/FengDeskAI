using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Enums.Notification;

namespace FengDeskAI.Domain.Entities.Announcement
{
    public class Notification : BaseEntity
    {
            public Guid UserId { get; set; }

            public NotificationType Type { get; set; }

            /// <summary>Tiêu đề ngắn hiển thị trên danh sách.</summary>
            public string Title { get; set; } = null!;

            /// <summary>Nội dung chi tiết.</summary>
            public string Message { get; set; } = null!;

            public bool IsRead { get; set; }
            public DateTime? ReadAt { get; set; }

            /// <summary>Id của đối tượng liên quan (orderId, deliveryId…) — để client deep-link.</summary>
            public Guid? ReferenceId { get; set; }

            /// <summary>Tên loại đối tượng liên quan ("Order", "Delivery"…).</summary>
            public string? ReferenceType { get; set; }
        

    }
}
