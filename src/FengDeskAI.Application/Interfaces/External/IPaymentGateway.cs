namespace FengDeskAI.Application.Interfaces.External;

public interface IPaymentGateway
{
    string Provider { get; }

    Task<PaymentLinkResult> CreatePaymentLinkAsync(PaymentLinkRequest request, CancellationToken ct = default);

    Task CancelPaymentLinkAsync(long orderCode, string? reason, CancellationToken ct = default);

    PaymentWebhookResult VerifyWebhook(string rawJsonBody);
}
