namespace FengDeskAI.Application.Interfaces.External;

public interface IPaymentGateway
{
    string Provider { get; }

    Task<PaymentLinkResult> CreatePaymentLinkAsync(PaymentLinkRequest request, CancellationToken ct = default);

    Task CancelPaymentLinkAsync(long orderCode, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Hoàn tiền cho một giao dịch đã thanh toán (đối soát qua orderCode). PayOS hiện chưa nối
    /// API refund thật — impl trả success giả lập để hoàn thiện luồng; thay impl khi có credential.
    /// </summary>
    Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken ct = default);

    PaymentWebhookResult VerifyWebhook(string rawJsonBody);
}
