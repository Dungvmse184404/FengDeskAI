using System.Text.Json;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Refund sub-saga (do Manager giám sát). Webhook cổng cập nhật kết quả; Manager retry/xác nhận thủ công/hủy.
/// Mọi can thiệp thủ công vào tiền đều có audit trail. Transition sai → HTTP 409.
/// </summary>
[Route("api/refunds")]
[Authorize]
public class RefundsController : ApiControllerBase
{
    private readonly IRefundService _service;

    public RefundsController(IRefundService service) => _service = service;

    /// <summary>Webhook cổng thanh toán báo kết quả hoàn tiền (verify chữ ký + idempotent).</summary>
    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOsRefundWebhook([FromBody] JsonElement payload, CancellationToken ct)
        => ToActionResult(await _service.HandleWebhookAsync(payload.GetRawText(), ct));

    /// <summary>Danh sách refund cần Manager để mắt (Failed / ManagerReview).</summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> GetForManager([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetForManagerAsync(page, ct));

    /// <summary>Manager retry thủ công một refund (Failed / ManagerReview).</summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
        => ToActionResult(await _service.RetryRefundAsync(id, RmaActor, ct));

    /// <summary>Manager xác nhận thủ công đã hoàn tiền — BẮT BUỘC manual_reason + evidence_url.</summary>
    [HttpPost("{id:guid}/manager-confirm")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> ManagerConfirm(Guid id, [FromBody] ManagerConfirmRefundRequest request, CancellationToken ct)
        => ToActionResult(await _service.ManagerConfirmRefundAsync(id, RmaActor, request, ct));

    /// <summary>Manager hủy refund khi phát hiện gian lận (chỉ khi Pending).</summary>
    [HttpPost("{id:guid}/manager-cancel")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> ManagerCancel(Guid id, CancellationToken ct)
        => ToActionResult(await _service.ManagerCancelRefundAsync(id, RmaActor, ct));
}
