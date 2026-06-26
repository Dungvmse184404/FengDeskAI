using System.Text.Json.Serialization;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Payload callback GHN gửi mỗi lần đổi trạng thái (POST JSON). GHN không ký/secret —
/// endpoint tự bảo vệ bằng query key. Chỉ map field cần để chuẩn hóa về <c>ShippingWebhookRequest</c>.
/// Xem Documents/GHN_INTEGRATION.md §6.
/// </summary>
public class GhnCallback
{
    /// <summary>create | switch_status | update_weight | update_cod | update_fee</summary>
    [JsonPropertyName("Type")] public string? Type { get; set; }
    [JsonPropertyName("OrderCode")] public string? OrderCode { get; set; }
    [JsonPropertyName("ClientOrderCode")] public string? ClientOrderCode { get; set; }
    [JsonPropertyName("Status")] public string? Status { get; set; }
    [JsonPropertyName("ShopID")] public long ShopId { get; set; }
    [JsonPropertyName("TotalFee")] public long TotalFee { get; set; }
    [JsonPropertyName("CODAmount")] public long CodAmount { get; set; }
}

/// <summary>Quy đổi trạng thái GHN → <see cref="DeliveryStatus"/> theo bảng §7.</summary>
public static class GhnStatusMapper
{
    public static DeliveryStatus Map(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "ready_to_pick" or "picking" or "money_collect_picking" => DeliveryStatus.Confirmed,
            "picked" or "storing" or "transporting" or "sorting"
                or "delivering" or "money_collect_delivering" => DeliveryStatus.Shipped,
            "delivered" => DeliveryStatus.Delivered,
            "delivery_fail" or "exception" or "damage" or "lost" => DeliveryStatus.DeliveryFailed,
            "waiting_to_return" or "return" or "return_transporting" or "return_sorting"
                or "returning" or "return_fail" or "returned" => DeliveryStatus.Returned,
            "cancel" => DeliveryStatus.Cancelled,
            _ => DeliveryStatus.Confirmed,
        };
}
