using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.Extensions.Options;

namespace FengDeskAI.Infrastructure.ExternalServices.Payment;

/// <summary>
/// Cổng thanh toán PayOS qua REST API (https://payos.vn/docs) + ký/verify HMAC-SHA256
/// bằng checksum key. Dùng REST thay SDK vì package SDK hiện tại (payOS v2) đổi API,
/// còn Net.payOS 1.x không còn trên NuGet.
/// </summary>
public class PayOsPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly PayOsSettings _settings;
    public string Provider => "PayOS";

    private const string CREATE_PAYMENT_URL = "/v2/payment-requests";
    private const string GET_PAYMENT_URL = "/v2/payment-requests/{0}";
    private const string CANCEL_PAYMENT_URL = "/v2/payment-requests/{0}/cancel";

    public PayOsPaymentGateway(HttpClient http, IOptions<PayOsSettings> options)
    {
        _settings = options.Value;
        _http = http;
        _http.BaseAddress = new Uri(_settings.BaseUrl);
        // Chỉ set header khi đã cấu hình — tránh ném khi PayOS chưa có key (gateway vẫn resolve được).
        if (!string.IsNullOrEmpty(_settings.ClientId))
            _http.DefaultRequestHeaders.Add("x-client-id", _settings.ClientId);
        if (!string.IsNullOrEmpty(_settings.ApiKey))
            _http.DefaultRequestHeaders.Add("x-api-key", _settings.ApiKey);
    }

    

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(PaymentLinkRequest request, CancellationToken ct = default)
    {
        // Chữ ký tạo link: HMAC trên 5 trường sắp xếp cố định (amount, cancelUrl, description, orderCode, returnUrl).
        var signature = HmacSha256(
            $"amount={request.Amount}&cancelUrl={_settings.CancelUrl}&description={request.Description}" +
            $"&orderCode={request.OrderCode}&returnUrl={_settings.ReturnUrl}");

        var body = new
        {
            orderCode = request.OrderCode,
            amount = request.Amount,
            description = request.Description,
            cancelUrl = _settings.CancelUrl,
            returnUrl = _settings.ReturnUrl,
            items = request.Items.Select(i => new { name = i.Name, quantity = i.Quantity, price = i.Price }),
            signature,
        };

        using var resp = await _http.PostAsJsonAsync(CREATE_PAYMENT_URL, body, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
        if (code != "00" || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            var desc = root.TryGetProperty("desc", out var d) ? d.GetString() : "unknown";
            throw new InvalidOperationException($"PayOS create payment link failed: {code} {desc}");
        }

        return new PaymentLinkResult(
            CheckoutUrl: data.GetProperty("checkoutUrl").GetString()!,
            PaymentLinkId: data.GetProperty("paymentLinkId").GetString()!,
            OrderCode: data.GetProperty("orderCode").GetInt64(),
            QrCode: data.TryGetProperty("qrCode", out var qr) ? qr.GetString() : null);
    }

    //public async Task CancelPaymentLinkAsync(long orderCode, string? reason, CancellationToken ct = default)
    //{
    //    var body = new { cancellationReason = string.IsNullOrWhiteSpace(reason) ? "Khách hủy thanh toán" : reason };
    //    using var resp = await _http.PostAsJsonAsync($"/v2/payment-requests/{orderCode}/cancel", body, ct);
    //    var json = await resp.Content.ReadAsStringAsync(ct);
    //    using var doc = JsonDocument.Parse(json);
    //    var root = doc.RootElement;

    //    var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
    //    if (code != "00")
    //    {
    //        var desc = root.TryGetProperty("desc", out var d) ? d.GetString() : "unknown";
    //        throw new InvalidOperationException($"PayOS cancel payment link failed: {code} {desc}");
    //    }
    //}

    public async Task CancelPaymentLinkAsync(long orderCode, string? reason = null, CancellationToken ct = default)
    {
        string endpoint = string.Format(CANCEL_PAYMENT_URL, orderCode);
        var body = new
        {
            cancellationReason = string.IsNullOrWhiteSpace(reason)
                ? "Khách hủy thanh toán"
                : reason
        };
        using var resp = await _http.PostAsJsonAsync(endpoint, body, ct);

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetString() : null;
        if (code != "00")
        {
            var desc = root.TryGetProperty("desc", out var d) ? d.GetString() : "Lỗi không xác định từ PayOS";
            throw new InvalidOperationException($"Không thể hủy giao dịch PayOS. Mã lỗi: {code} - Chi tiết: {desc}");
        }
    }

    /// <summary>
    /// PayOS chưa mở API hoàn tiền tự động cho tích hợp REST hiện tại — trả success giả lập kèm
    /// mã tham chiếu để luồng nghiệp vụ chạy trọn vẹn. Khi có API/credential refund thật, thay
    /// phần thân bằng lời gọi HTTP tương tự CreatePaymentLinkAsync.
    /// </summary>
    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        // Mã tham chiếu suy ra TẤT ĐỊNH từ idempotency key → gọi lại cùng key trả cùng kết quả
        // (mô phỏng hành vi idempotent của cổng thật). Khi có API refund thật, thay bằng lời gọi HTTP.
        var refundId = request.GatewayRef ?? $"REFUND-{request.IdempotencyKey}";
        return Task.FromResult(new RefundResult(
            Success: true,
            ProviderRefundId: refundId,
            Code: "00",
            Message: "Đã ghi nhận hoàn tiền (giả lập)."));
    }

    public PaymentWebhookResult VerifyWebhook(string rawJsonBody)
    {
        using var doc = JsonDocument.Parse(rawJsonBody);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Webhook thiếu trường data.");
        var providedSignature = root.TryGetProperty("signature", out var s) ? s.GetString() : null;
        if (string.IsNullOrEmpty(providedSignature))
            throw new InvalidOperationException("Webhook thiếu chữ ký.");

        // Verify: HMAC trên các trường của data sắp xếp theo key (alphabet), dạng k=v&...
        var expected = HmacSha256(BuildSortedQuery(data));
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(providedSignature)))
            throw new InvalidOperationException("Chữ ký webhook không hợp lệ.");

        var code = data.TryGetProperty("code", out var dc) ? dc.GetString() : null;
        return new PaymentWebhookResult(
            Success: code == "00",
            OrderCode: data.GetProperty("orderCode").GetInt64(),
            Amount: data.TryGetProperty("amount", out var a) ? a.GetInt32() : 0,
            ProviderReference: data.TryGetProperty("reference", out var r) ? r.GetString() : null,
            Code: code,
            Description: data.TryGetProperty("desc", out var dd) ? dd.GetString() : null);
    }

    private static string BuildSortedQuery(JsonElement data)
    {
        var pairs = data.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={ValueToString(p.Value)}");
        return string.Join("&", pairs);
    }

    private static string ValueToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Null => "",
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => v.GetRawText(),
    };

    private string HmacSha256(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_settings.ChecksumKey));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
