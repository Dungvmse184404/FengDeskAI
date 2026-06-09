using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Domain.Enums.Sales;

namespace FengDeskAI.Application.Features.Payment.DTOs;

public class CreatePaymentResponse
{
    public Guid OrderId { get; set; }
    public long OrderCode { get; set; }
    public decimal Amount { get; set; }
    public string CheckoutUrl { get; set; } = null!;
    public string? QrCode { get; set; }
    public string PaymentLinkId { get; set; } = null!;
    public PaymentStatus Status { get; set; }
}

public class PaymentStatusResponse
{
    public Guid OrderId { get; set; }
    public OrderStatus OrderStatus { get; set; }
    public long? OrderCode { get; set; }
    public PaymentStatus? PaymentStatus { get; set; }
    public decimal? Amount { get; set; }
    public string? ProviderTransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
}
