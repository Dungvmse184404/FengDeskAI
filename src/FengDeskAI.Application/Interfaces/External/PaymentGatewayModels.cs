namespace FengDeskAI.Application.Interfaces.External;

public record PaymentLineItem(string Name, int Quantity, int Price);

public record PaymentLinkRequest(long OrderCode, int Amount, string Description, IReadOnlyList<PaymentLineItem> Items);

public record PaymentLinkResult(string CheckoutUrl, string PaymentLinkId, long OrderCode, string? QrCode);

public record PaymentWebhookResult(bool Success, long OrderCode, int Amount, string? ProviderReference, string? Code, string? Description);
