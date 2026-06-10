using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Payment.DTOs;

namespace FengDeskAI.Application.Features.Payment.Services;

public interface IPaymentService
{
    /// <summary>Tạo link thanh toán PayOS cho một order Pending của user.</summary>
    Task<IServiceResult<CreatePaymentResponse>> CreatePaymentAsync(Guid orderId, Guid userId, CancellationToken ct = default);

    /// <summary>Xử lý webhook PayOS (raw JSON đã verify chữ ký): cập nhật giao dịch + order + tạo shipment.</summary>
    Task<IServiceResult> HandleWebhookAsync(string rawJsonBody, CancellationToken ct = default);

    /// <summary>
    /// Hủy thanh toán: hủy link PayOS + chuyển transaction/order/deliveries sang Cancelled + hoàn kho.
    /// Chỉ áp dụng khi đơn đang chờ thanh toán (chưa Paid).
    /// </summary>
    Task<IServiceResult<PaymentStatusResponse>> CancelPaymentAsync(Guid orderId, Guid userId, string? reason, CancellationToken ct = default);

    Task<IServiceResult<PaymentStatusResponse>> GetStatusAsync(Guid orderId, Guid userId, CancellationToken ct = default);

    /// <summary>[DEV only] Giả lập thanh toán thành công (bỏ qua PayOS) để test luồng sau thanh toán.</summary>
    Task<IServiceResult<PaymentStatusResponse>> SimulatePaidAsync(Guid orderId, Guid userId, CancellationToken ct = default);
}
