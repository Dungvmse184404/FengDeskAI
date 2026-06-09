using System.Text.Json;
using FengDeskAI.Application.Features.Payment.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Webhook thanh toán. PayOS POST về đây; chữ ký được verify bằng checksum key trong service.</summary>
[Route("api/payments")]
public class PaymentsController : ApiControllerBase
{
    private readonly IPaymentService _payment;

    public PaymentsController(IPaymentService payment) => _payment = payment;

    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOsWebhook([FromBody] JsonElement payload, CancellationToken ct)
        => ToActionResult(await _payment.HandleWebhookAsync(payload.GetRawText(), ct));
}
