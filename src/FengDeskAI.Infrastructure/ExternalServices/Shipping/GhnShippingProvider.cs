using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Nhà vận chuyển GHN (toàn quốc). Định tuyến theo mã quận/phường GHN (không dùng lat/lng).
/// Header <c>Token</c> cấu hình ở DI; header <c>ShopId</c> gắn theo từng request để chọn địa chỉ gửi
/// của store (multi-vendor). Gửi <c>client_order_code = delivery.Id</c> làm khóa idempotent + khớp callback.
/// Hỗ trợ: tạo vận đơn, ước tính phí (/fee), yêu cầu giao lại (/switch-status/storing).
/// Xem Documents/GHN_INTEGRATION.md §4, §5, §9.
/// </summary>
public class GhnShippingProvider : IShippingProvider
{
    /// <summary>Cho xem hàng, không cho thử. (KHONGCHOXEMHANG | CHOXEMHANGKHONGTHU | CHOTHUHANG)</summary>
    private const string RequiredNote = "CHOXEMHANGKHONGTHU";
    private const decimal MaxInsuranceValue = 5_000_000m;
    private const string CreatePath = "/shiip/public-api/v2/shipping-order/create";
    private const string FeePath = "/shiip/public-api/v2/shipping-order/fee";
    private const string RedeliverPath = "/shiip/public-api/v2/switch-status/storing"; // "Giao lại" — GHN doc id=65

    private readonly HttpClient _http;
    private readonly GhnSettings _cfg;
    private readonly ILogger<GhnShippingProvider> _logger;

    public string Name => "GHN";

    public GhnShippingProvider(HttpClient http, IOptions<GhnSettings> options, ILogger<GhnShippingProvider> logger)
    {
        _http = http;
        _cfg = options.Value;
        _logger = logger;
    }

    public async Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest req, CancellationToken ct = default)
    {
        var shopId = ResolveShopId(req);
        if (req.ToDistrictId is null || string.IsNullOrEmpty(req.ToWardCode))
            throw new InvalidOperationException("GHN thiếu mã quận/phường điểm giao (chưa đồng bộ GhnDistrictId/GhnWardCode).");

        var data = await SendAsync<CreateOrderData>(CreatePath, BuildCreateBody(req), shopId, ct, $"tạo vận đơn delivery {req.DeliveryId}");
        if (string.IsNullOrEmpty(data.OrderCode))
            throw new InvalidOperationException("GHN trả về order_code rỗng.");

        _logger.LogInformation("[GHN] Tạo vận đơn {OrderCode} cho delivery {DeliveryId} (phí {Fee}).",
            data.OrderCode, req.DeliveryId, data.TotalFee);

        return new ShipmentResult(
            Provider: Name,
            ProviderOrderId: data.OrderCode,
            TrackingCode: data.OrderCode,
            EstimatedDeliveryDate: data.ExpectedDeliveryTime,
            TrackingUrl: $"https://donhang.ghn.vn/?order_code={data.OrderCode}",
            ShippingFee: ParseFee(data.TotalFee));
    }

    public async Task<decimal?> EstimateFeeAsync(ShipmentRequest req, CancellationToken ct = default)
    {
        if (req.ToDistrictId is null || string.IsNullOrEmpty(req.ToWardCode))
            return null; // chưa đủ mã vùng GHN → để caller fallback sang calculator

        var shopId = ResolveShopId(req);
        var data = await SendAsync<FeeData>(FeePath, BuildFeeBody(req), shopId, ct, "ước tính phí");
        return data.Total;
    }

    public async Task<bool> RedeliverAsync(string providerOrderCode, int? shopId, CancellationToken ct = default)
    {
        var shop = shopId ?? _cfg.DefaultShopId;
        if (shop <= 0 || string.IsNullOrEmpty(providerOrderCode)) return false;

        using var res = await PostAsync(RedeliverPath, new { order_codes = new[] { providerOrderCode } }, shop, ct,
            $"giao lại vận đơn {providerOrderCode}");
        _logger.LogInformation("[GHN] Yêu cầu giao lại vận đơn {OrderCode}.", providerOrderCode);
        return true;
    }

    private int ResolveShopId(ShipmentRequest req)
    {
        var shopId = req.ShopId ?? _cfg.DefaultShopId;
        if (shopId <= 0)
            throw new InvalidOperationException("GHN thiếu ShopId: store chưa cấu hình GhnShopId và Ghn:DefaultShopId trống.");
        return shopId;
    }

    private async Task<HttpResponseMessage> PostAsync(string path, object body, int shopId, CancellationToken ct, string action)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        msg.Headers.Add("ShopId", shopId.ToString(CultureInfo.InvariantCulture));

        var res = await _http.SendAsync(msg, ct);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("[GHN] {Action} lỗi {Status}: {Body}", action, (int)res.StatusCode, err);
            res.Dispose();
            throw new HttpRequestException($"GHN {action} thất bại ({(int)res.StatusCode}).");
        }
        return res;
    }

    private async Task<T> SendAsync<T>(string path, object body, int shopId, CancellationToken ct, string action)
    {
        using var res = await PostAsync(path, body, shopId, ct, action);
        var dto = await res.Content.ReadFromJsonAsync<GhnResponse<T>>(cancellationToken: ct);
        if (dto is null || dto.Data is null)
            throw new InvalidOperationException($"GHN {action}: response rỗng.");
        return dto.Data;
    }

    private object BuildCreateBody(ShipmentRequest req) => new
    {
        payment_type_id = 1,                                    // shop trả phí ship (đã thu online)
        required_note = RequiredNote,
        client_order_code = req.DeliveryId.ToString(),         // khóa idempotent + khớp callback
        // điểm gửi: bỏ qua nếu thiếu → GHN lấy theo địa chỉ của ShopId
        from_name = NullIfEmpty(req.PickupName),
        from_phone = NullIfEmpty(req.PickupPhone),
        from_address = NullIfEmpty(req.PickupAddress),
        from_ward_code = req.FromWardCode,
        from_district_id = req.FromDistrictId,
        // điểm giao (khách)
        to_name = req.RecipientName,
        to_phone = req.RecipientPhone,
        to_address = req.ShippingAddress,
        to_ward_code = req.ToWardCode,
        to_district_id = req.ToDistrictId,
        cod_amount = (long)req.CodAmount,                       // 0 khi đã thu online
        insurance_value = (long)Math.Min(req.Subtotal, MaxInsuranceValue),
        service_type_id = req.ServiceTypeId ?? _cfg.DefaultServiceTypeId,
        weight = req.TotalWeightGram,
        length = req.LengthCm,
        width = req.WidthCm,
        height = req.HeightCm,
        items = req.Items.Select(BuildItem),
    };

    private object BuildFeeBody(ShipmentRequest req) => new
    {
        from_district_id = req.FromDistrictId,
        from_ward_code = req.FromWardCode,
        to_district_id = req.ToDistrictId,
        to_ward_code = req.ToWardCode,
        service_type_id = req.ServiceTypeId ?? _cfg.DefaultServiceTypeId,
        insurance_value = (long)Math.Min(req.Subtotal, MaxInsuranceValue),
        weight = req.TotalWeightGram,
        length = req.LengthCm,
        width = req.WidthCm,
        height = req.HeightCm,
        items = req.Items.Select(BuildItem),
    };

    private static object BuildItem(ShipmentItem i) => new
    {
        name = i.Name,
        code = i.Id,
        quantity = i.Quantity,
        price = (long)i.Price,
        weight = i.WeightGram,
        length = i.LengthCm,
        width = i.WidthCm,
        height = i.HeightCm,
    };

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    private static decimal? ParseFee(string? raw)
        => decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;

    private sealed record GhnResponse<T>(
        [property: JsonPropertyName("code")] int Code,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("data")] T? Data);

    private sealed record CreateOrderData(
        [property: JsonPropertyName("order_code")] string OrderCode,
        [property: JsonPropertyName("total_fee")] string? TotalFee,
        [property: JsonPropertyName("expected_delivery_time")] DateTime? ExpectedDeliveryTime);

    private sealed record FeeData(
        [property: JsonPropertyName("total")] decimal Total,
        [property: JsonPropertyName("service_fee")] decimal ServiceFee);
}
