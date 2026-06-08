using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Sales;

/// <summary>Nhật ký chuyển trạng thái cấp order.</summary>
public class OrderStatusLog : BaseEntity
{
    public Guid OrderId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = null!;
    public Guid? ChangedBy { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }

    public Order Order { get; set; } = null!;
}
