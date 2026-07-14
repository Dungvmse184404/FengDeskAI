namespace FengDeskAI.Application.Interfaces.External;

public record PaymentLineItem(string Name, int Quantity, int Price);

public record PaymentLinkRequest(long OrderCode, int Amount, string Description, IReadOnlyList<PaymentLineItem> Items);

public record PaymentLinkResult(string CheckoutUrl, string PaymentLinkId, long OrderCode, string? QrCode);

public record PaymentWebhookResult(bool Success, long OrderCode, int Amount, string? ProviderReference, string? Code, string? Description);

/// <summary>
/// Yêu cầu hoàn tiền. <paramref name="IdempotencyKey"/> BẮT BUỘC — cổng phải trả cùng kết quả cho
/// cùng key (retry an toàn, không hoàn trùng). <paramref name="GatewayRef"/> là mã tham chiếu lần gọi trước (nếu retry).
/// </summary>
public record RefundRequest(long OrderCode, int Amount, string Reason, string IdempotencyKey, string? GatewayRef = null);

public record RefundResult(bool Success, string? ProviderRefundId, string? Code, string? Message);
