using System.Text.Json;
using System.ComponentModel.DataAnnotations;
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

    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, RmaActor, ct));

    /// <summary>Manager retry thủ công một refund (Failed / ManagerReview).</summary>
    [HttpPost("{id:guid}/retry")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
        => ToActionResult(await _service.RetryRefundAsync(id, RmaActor, ct));

    /// <summary>Manager xác nhận thủ công đã hoàn tiền — multipart/form-data, bắt buộc manualReason + evidenceFile.</summary>
    [HttpPost("{id:guid}/manager-confirm")]
    [Consumes("multipart/form-data")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> ManagerConfirm(Guid id, [FromForm] ManagerConfirmRefundFormModel form, CancellationToken ct)
    {
        Stream? evidenceStream = null;
        try
        {
            RefundEvidenceFile? evidenceFile = null;
            if (form.EvidenceFile is not null)
            {
                evidenceStream = form.EvidenceFile.OpenReadStream();
                evidenceFile = new RefundEvidenceFile(
                    evidenceStream, form.EvidenceFile.FileName, form.EvidenceFile.ContentType);
            }

            var request = new ManagerConfirmRefundRequest
            {
                ManualReason = form.ManualReason,
                EvidenceFile = evidenceFile,
            };
            return ToActionResult(await _service.ManagerConfirmRefundAsync(id, RmaActor, request, ct));
        }
        finally
        {
            if (evidenceStream is not null)
                await evidenceStream.DisposeAsync();
        }
    }

    /// <summary>Manager hủy refund khi phát hiện gian lận (chỉ khi Pending).</summary>
    [HttpPost("{id:guid}/manager-cancel")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> ManagerCancel(Guid id, CancellationToken ct)
        => ToActionResult(await _service.ManagerCancelRefundAsync(id, RmaActor, ct));
}

/// <summary>Binding model upload ảnh bằng chứng khi Manager xác nhận đã hoàn tiền thủ công.</summary>
public sealed class ManagerConfirmRefundFormModel
{
    [Required]
    public string ManualReason { get; set; } = null!;

    [Required]
    public IFormFile? EvidenceFile { get; set; }
}
