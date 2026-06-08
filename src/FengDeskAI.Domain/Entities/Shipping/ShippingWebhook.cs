using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Shipping;

/// <summary>
/// Bản ghi thô mỗi callback từ nhà vận chuyển — lưu trước, xử lý sau (idempotent retry).
/// </summary>
public class ShippingWebhook : BaseEntity
{
    public string? Provider { get; set; }
    public string? EventType { get; set; }

    /// <summary>Payload thô (JSONB).</summary>
    public string Payload { get; set; } = null!;

    public bool IsProcessed { get; set; }
    public DateTime ReceivedAt { get; set; }
}
