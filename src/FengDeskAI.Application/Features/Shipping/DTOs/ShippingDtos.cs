using FengDeskAI.Domain.Enums.Sales;
using FengDeskAI.Domain.Enums.Shipping;

namespace FengDeskAI.Application.Features.Shipping.DTOs;

/// <summary>
/// Payload chuẩn hóa từ nhà vận chuyển. Khớp delivery theo DeliveryId hoặc (Provider + ProviderOrderId).
/// </summary>
public class ShippingWebhookRequest
{
    public string? Provider { get; set; }
    public string? EventType { get; set; }
    public Guid? DeliveryId { get; set; }
    public string? ProviderOrderId { get; set; }
    public DeliveryStatus NewStatus { get; set; }
    public string? TrackingCode { get; set; }
    /// <summary>Payload thô (JSON string) — lưu nguyên vào log/raw_payload.</summary>
    public string? RawPayload { get; set; }
}

public class DeliveryProgressLogResponse
{
    public Guid Id { get; set; }
    public Guid DeliveryId { get; set; }
    public DeliverySource SourceType { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime LoggedAt { get; set; }
}
