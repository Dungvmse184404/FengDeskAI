using System.Text.Json;
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
        if (!IsWebhookAuthorized())
            return Unauthorized(ServiceResult.Failure(ApiStatusCodes.Unauthorized, "Webhook secret không hợp lệ."));

        return ToActionResult(await _service.ProcessWebhookAsync(request, ct));
    }

    /// <summary>
    /// Callback AhaMove: chuẩn hóa payload riêng của AhaMove về <see cref="ShippingWebhookRequest"/>
    /// rồi gọi chung pipeline. Khớp delivery theo tracking_number (= delivery.Id) ta đã gửi, fallback
    /// theo (Provider + ProviderOrderId). Xem Documents/AHAMOVE_INTEGRATION.md §6.
    /// </summary>
    [HttpPost("ahamove/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> AhamoveWebhook([FromBody] AhamoveCallback cb, CancellationToken ct)
    {
        if (!IsWebhookAuthorized())
            return Unauthorized(ServiceResult.Failure(ApiStatusCodes.Unauthorized, "Webhook secret không hợp lệ."));

        Guid? deliveryId = Guid.TryParse(cb.DropOff?.TrackingNumber, out var id) ? id : null;
        var request = new ShippingWebhookRequest
        {
            Provider = "Ahamove",
            EventType = cb.Status,
            DeliveryId = deliveryId,
            ProviderOrderId = cb.Id,
            NewStatus = AhamoveStatusMapper.Map(cb),
            TrackingCode = cb.Id,
            TrackingUrl = cb.SharedLink,
            RawPayload = JsonSerializer.Serialize(cb),
        };
        return ToActionResult(await _service.ProcessWebhookAsync(request, ct));
    }

    /// <summary>
    /// Callback GHN: GHN không gửi được header secret nên endpoint tự bảo vệ bằng query <c>key</c>
    /// (so với ShippingWebhook:Secret). Chuẩn hóa payload GHN về <see cref="ShippingWebhookRequest"/> rồi
    /// gọi chung pipeline. Khớp delivery theo ClientOrderCode (= delivery.Id), fallback (Provider + OrderCode).
    /// Chỉ Type create/switch_status mới đẩy state machine. Xem Documents/GHN_INTEGRATION.md §6.
    /// </summary>
    [HttpPost("ghn/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> GhnWebhook([FromBody] GhnCallback cb, [FromQuery] string? key, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_settings.Secret) || key != _settings.Secret)
            return Unauthorized(ServiceResult.Failure(ApiStatusCodes.Unauthorized, "Webhook secret không hợp lệ."));

        // update_fee / update_weight / update_cod không đổi trạng thái giao → trả 200 để GHN không retry.
        if (cb.Type is not ("create" or "switch_status"))
            return Ok(ServiceResult.Success("Bỏ qua sự kiện không đổi trạng thái."));

        Guid? deliveryId = Guid.TryParse(cb.ClientOrderCode, out var id) ? id : null;
        var request = new ShippingWebhookRequest
        {
            Provider = "GHN",
            EventType = cb.Type,
            DeliveryId = deliveryId,
            ProviderOrderId = cb.OrderCode,
            NewStatus = GhnStatusMapper.Map(cb.Status),
            TrackingCode = cb.OrderCode,
            RawPayload = JsonSerializer.Serialize(cb),
        };
        return ToActionResult(await _service.ProcessWebhookAsync(request, ct));
    }

    private bool IsWebhookAuthorized()
    {
        var provided = Request.Headers[SecretHeader].ToString();
        return !string.IsNullOrEmpty(_settings.Secret) && provided == _settings.Secret;
    }

    [HttpGet("deliveries/{deliveryId:guid}/progress")]
    public async Task<IActionResult> GetProgress(Guid deliveryId, CancellationToken ct)
        => ToActionResult(await _service.GetProgressLogsAsync(deliveryId, CurrentUserId, IsAdmin, ct));

    /// <summary>
    /// Yêu cầu nhà vận chuyển giao lại một đơn giao thất bại (owner/staff store hoặc admin).
    /// Xem Documents/GHN_INTEGRATION.md §9.2.
    /// </summary>
    [HttpPost("deliveries/{deliveryId:guid}/redeliver")]
    public async Task<IActionResult> Redeliver(Guid deliveryId, CancellationToken ct)
        => ToActionResult(await _service.RedeliverAsync(deliveryId, CurrentUserId, IsAdmin, ct));
}
  