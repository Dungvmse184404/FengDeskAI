using FengDeskAI.Domain.Common;
using FengDeskAI.Domain.Entities.Sales;
using FengDeskAI.Domain.Enums.Shipping;

namespace FengDeskAI.Domain.Entities.Shipping;

/// <summary>
/// Nhật ký tiến trình giao hàng của một delivery — ghi mỗi lần đổi trạng thái,
/// kèm raw payload nếu đến từ webhook nhà vận chuyển.
/// </summary>
public class DeliveryProgressLog : BaseEntity
{
    public Guid DeliveryId { get; set; }
    public DeliverySource SourceType { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }

    /// <summary>Payload thô từ provider (JSONB) — null nếu cập nhật tay.</summary>
    public string? RawPayload { get; set; }

    public string? Note { get; set; }
    public DateTime LoggedAt { get; set; }

    public Delivery Delivery { get; set; } = null!;
}
