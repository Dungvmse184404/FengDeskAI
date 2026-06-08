using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Shipping.DTOs;
using FengDeskAI.Application.Features.Shipping.Services;
using FengDeskAI.Infrastructure.ExternalServices.Shipping;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Tích hợp vận chuyển. Endpoint webhook là anonymous nhưng yêu cầu header secret
/// (<c>X-Webhook-Secret</c>). Xem tiến trình giao hàng yêu cầu owner/staff store hoặc admin.
/// </summary>
[Route("api/shipping")]
[Authorize]
public class ShippingController : ApiControllerBase
{
    private const string SecretHeader = "X-Webhook-Secret"; // thêm vào settings sau

    private readonly IShippingService _service;
    private readonly ShippingWebhookSettings _settings;

    public ShippingController(IShippingService service, IOptions<ShippingWebhookSettings> settings)
    {
        _service = service;
        _settings = settings.Value;
    }

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] ShippingWebhookRequest request, CancellationToken ct)
    {
        var provided = Request.Headers[SecretHeader].ToString();
        if (string.IsNullOrEmpty(_settings.Secret) || provided != _settings.Secret)
            return Unauthorized(ServiceResult.Failure(ApiStatusCodes.Unauthorized, "Webhook secret không hợp lệ."));

        return ToActionResult(await _service.ProcessWebhookAsync(request, ct));
    }

    [HttpGet("deliveries/{deliveryId:guid}/progress")]
    public async Task<IActionResult> GetProgress(Guid deliveryId, CancellationToken ct)
        => ToActionResult(await _service.GetProgressLogsAsync(deliveryId, CurrentUserId, IsAdmin, ct));
}
