using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Logging;

namespace FengDeskAI.Infrastructure.ExternalServices.Shipping;

/// <summary>
/// Nhà vận chuyển AhaMove (giao nội thành). Tạo vận đơn qua <c>POST /v3/orders</c>:
/// path[0] = điểm lấy hàng (store), path[1] = điểm giao (khách). Gửi <c>tracking_number = delivery.Id</c>
/// làm khóa idempotent + để khớp callback. Token cache qua <see cref="IAhamoveTokenProvider"/>,
/// tự refresh & retry một lần khi gặp 401. Xem Documents/AHAMOVE_INTEGRATION.md §3, §5.
/// </summary>
public class AhamoveShippingProvider : IShippingProvider
{
    private readonly HttpClient _http;
    private readonly IAhamoveTokenProvider _token;
    private readonly ILogger<AhamoveShippingProvider> _logger;

    public string Name => "Ahamove";

    public AhamoveShippingProvider(HttpClient http, IAhamoveTokenProvider token, ILogger<AhamoveShippingProvider> logger)
    {
        _http = http;
        _token = token;
        _logger = logger;
    }

    public async Task<ShipmentResult> CreateShipmentAsync(ShipmentRequest req, CancellationToken ct = default)
    {
        var body = BuildBody(req);

        var res = await SendAsync(body, await _token.GetAsync(ct), ct);
        if (res.StatusCode == HttpStatusCode.Unauthorized)   // token hết hạn → refresh & thử lại 1 lần
        {
            res.Dispose();
            res = await SendAsync(body, await _token.RefreshAsync(ct), ct);
        }

        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync(ct);
            _logger.LogError("[Ahamove] Tạo vận đơn delivery {DeliveryId} lỗi {Status}: {Body}",
                req.DeliveryId, (int)res.StatusCode, err);
            res.Dispose();
            throw new HttpRequestException($"AhaMove tạo vận đơn thất bại ({(int)res.StatusCode}).");
        }

        var dto = await res.Content.ReadFromJsonAsync<CreateOrderResponse>(cancellationToken: ct);
        res.Dispose();
        if (dto is null || string.IsNullOrEmpty(dto.OrderId))
            throw new InvalidOperationException("AhaMove trả về order_id rỗng.");

        _logger.LogInformation("[Ahamove] Tạo vận đơn {OrderId} cho delivery {DeliveryId} (status {Status}).",
            dto.OrderId, req.DeliveryId, dto.Status);

        return new ShipmentResult(
            Provider: Name,
            ProviderOrderId: dto.OrderId,
            TrackingCode: dto.OrderId,
            EstimatedDeliveryDate: DateTime.UtcNow.AddHours(3),
            TrackingUrl: dto.SharedLink);
    }

    private Task<HttpResponseMessage> SendAsync(object body, string token, CancellationToken ct)
    {
        var msg = new HttpRequestMessage(HttpMethod.Post, "/v3/orders") { Content = JsonContent.Create(body) };
        msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http.SendAsync(msg, ct);
    }

    private static object BuildBody(ShipmentRequest req) => new
    {
        order_time = 0,                                                      // 0 = giao ngay
        service_id = req.ServiceId,                                         // vd "SGN-BIKE"
        payment_method = req.CodAmount > 0 ? "CASH_BY_RECIPIENT" : "BALANCE",
        path = new object[]
        {
            new
            {
                lat = req.PickupLat, lng = req.PickupLng, address = req.PickupAddress,
                name = req.PickupName, mobile = req.PickupPhone,
                remarks = $"Đơn FengDesk: {req.OrderId}",
            },
            new
            {
                lat = req.RecipientLat, lng = req.RecipientLng, address = req.ShippingAddress,
                name = req.RecipientName, mobile = req.RecipientPhone,
                cod = req.CodAmount, item_value = req.Subtotal,
                tracking_number = req.DeliveryId.ToString(),               // khóa idempotent + khớp callback
            },
        },
        items = req.Items.Select(i => new { _id = i.Id, name = i.Name, price = i.Price, num = i.Quantity }),
        package_detail = new[] { new { weight = req.TotalWeightGram / 1000.0, description = "Đồ phong thủy" } },
    };

    private sealed record CreateOrderResponse(
        [property: JsonPropertyName("order_id")] string OrderId,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("shared_link")] string? SharedLink);
}
