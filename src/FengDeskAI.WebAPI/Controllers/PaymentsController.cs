using System.Text.Json;
using FengDeskAI.Application.Features.Payment.DTOs;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Thanh toán đơn hàng qua PayOS. Tạo link / xem trạng thái / hủy thanh toán theo order
/// của user đang đăng nhập; endpoint webhook là anonymous (verify chữ ký trong service).
/// </summary>
[Route("api/payments")]
[Authorize]
public class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _payment;

    public PaymentsController(IPaymentService payment) => _payment = payment;

    /// <summary>Tạo link thanh toán PayOS cho một order Pending (trả checkoutUrl + qrCode).</summary>
    [HttpPost("{orderId:guid}")]
    public async Task<IActionResult> Create(Guid orderId, CancellationToken ct)
        => ToActionResult(await _payment.CreatePaymentAsync(orderId, CurrentUserId, ct));

    /// <summary>Trạng thái thanh toán của đơn hàng.</summary>
    [HttpGet("{orderId:guid}")]
    public async Task<IActionResult> GetStatus(Guid orderId, CancellationToken ct)
        => ToActionResult(await _payment.GetStatusAsync(orderId, CurrentUserId, ct));

    /// <summary>Hủy thanh toán: hủy link PayOS + chuyển order/deliveries/transaction sang Cancelled + hoàn kho.</summary>
    [HttpPost("{orderId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid orderId, [FromBody] CancelPaymentRequest? request, CancellationToken ct)
        => ToActionResult(await _payment.CancelPaymentAsync(orderId, CurrentUserId, request?.Reason, ct));

    /// <summary>Webhook PayOS gọi về khi trạng thái thanh toán thay đổi.</summary>
    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOsWebhook([FromBody] JsonElement payload, CancellationToken ct)
        => ToActionResult(await _payment.HandleWebhookAsync(payload.GetRawText(), ct));
}
