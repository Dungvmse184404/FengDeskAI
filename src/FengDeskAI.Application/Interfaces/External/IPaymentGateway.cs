namespace FengDeskAI.Application.Interfaces.External;

public record PaymentLineItem(string Name, int Quantity, int Price);

/// <summary>Yêu cầu tạo link thanh toán (return/cancel URL do gateway tự thêm từ config).</summary>
public record PaymentLinkRequest(long OrderCode, int Amount, string Description, IReadOnlyList<PaymentLineItem> Items);

public record PaymentLinkResult(string CheckoutUrl, string PaymentLinkId, long OrderCode, string? QrCode);

/// <summary>Kết quả parse + verify chữ ký webhook từ cổng thanh toán.</summary>
public record PaymentWebhookResult(bool Success, long OrderCode, int Amount, string? ProviderReference, string? Code, string? Description);

/// <summary>Trừu tượng cổng thanh toán (impl: PayOS). Application không phụ thuộc SDK cụ thể.</summary>
public interface IPaymentGateway
{
    string Provider { get; }

    Task<PaymentLinkResult> CreatePaymentLinkAsync(PaymentLinkRequest request, CancellationToken ct = default);

    /// <summary>Parse body webhook (JSON thô) + verify chữ ký. Ném nếu chữ ký không hợp lệ.</summary>
    PaymentWebhookResult VerifyWebhook(string rawJsonBody);
}
