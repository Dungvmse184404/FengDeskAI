using System.Text.Json.Serialization;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Payload callback AhaMove gửi mỗi lần đổi trạng thái (POST nguyên JSON đơn). Chỉ map các field
/// cần để chuẩn hóa về <c>ShippingWebhookRequest</c>. Xem Documents/AHAMOVE_INTEGRATION.md §6.
/// </summary>
public class AhamoveCallback
{
    [JsonPropertyName("_id")] public string Id { get; set; } = null!;
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("sub_status")] public string? SubStatus { get; set; }
    [JsonPropertyName("service_id")] public string? ServiceId { get; set; }
    [JsonPropertyName("cancel_by_user")] public bool CancelByUser { get; set; }
    [JsonPropertyName("shared_link")] public string? SharedLink { get; set; }
    [JsonPropertyName("path")] public List<AhamovePathPoint>? Path { get; set; }

    /// <summary>Điểm giao (drop-off) = phần tử cuối của path; mang kết quả giao + tracking_number của ta.</summary>
    public AhamovePathPoint? DropOff => Path is { Count: > 0 } ? Path[^1] : null;
}

public class AhamovePathPoint
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("tracking_number")] public string? TrackingNumber { get; set; }
}

/// <summary>Quy đổi trạng thái AhaMove → <see cref="DeliveryStatus"/> theo bảng §4.</summary>
public static class AhamoveStatusMapper
{
    public static DeliveryStatus Map(AhamoveCallback cb)
    {
        var sub = cb.SubStatus?.Trim().ToUpperInvariant();
        if (sub == "RETURNED") return DeliveryStatus.Returned;

        var status = cb.Status?.Trim().ToUpperInvariant();
        var dropOff = cb.DropOff?.Status?.Trim().ToUpperInvariant();

        return status switch
        {
            "IDLE" or "ASSIGNING" or "ACCEPTED" or "CONFIRMING" or "PAYING" => DeliveryStatus.Confirmed,
            "IN PROCESS" or "IN_PROCESS" => DeliveryStatus.Shipped,
            "COMPLETED" => dropOff == "FAILED" ? DeliveryStatus.DeliveryFailed : DeliveryStatus.Delivered,
            "CANCELLED" => DeliveryStatus.Cancelled,
            "FAILED" => DeliveryStatus.DeliveryFailed,
            _ => DeliveryStatus.Confirmed,
        };
    }
}
