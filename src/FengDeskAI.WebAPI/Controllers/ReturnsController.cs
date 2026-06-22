using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Trả hàng / hoàn tiền / đổi trả (RMA).
/// Customer: tạo yêu cầu, xem yêu cầu của mình, hủy, gửi hàng trả.
/// Vendor (owner/staff store): xem & xử lý yêu cầu của delivery thuộc store mình (duyệt/từ chối/nhận hàng/xử lý).
/// Admin: xem toàn bộ, can thiệp, xác nhận hoàn tiền.
/// </summary>
[Route("api/returns")]
[Authorize]
public class ReturnsController : ApiControllerBase
{
    private readonly IReturnService _service;

    public ReturnsController(IReturnService service) => _service = service;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    // ----- Customer -----

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, page, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ToActionResult(await _service.CancelAsync(id, CurrentUserId, ct));

    [HttpPost("{id:guid}/ship-back")]
    public async Task<IActionResult> ShipBack(Guid id, [FromBody] ShipBackRequest request, CancellationToken ct)
        => ToActionResult(await _service.ShipBackAsync(id, CurrentUserId, request, ct));

    // ----- Vendor / Admin -----

    /// <summary>Tất cả yêu cầu trả hàng (paged) — chỉ admin.</summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetAll([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetAllAsync(page, ct));

    /// <summary>Yêu cầu trả hàng của một store (màn vendor). Yêu cầu owner/staff store đó hoặc admin.</summary>
    [HttpGet("stores/{storeId:guid}")]
    public async Task<IActionResult> GetForStore(Guid storeId, [FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetForStoreAsync(storeId, CurrentUserId, IsAdmin, page, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, CurrentUserId, IsAdmin, ct));

    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveReturnRequest request, CancellationToken ct)
        => ToActionResult(await _service.ApproveAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectReturnRequest request, CancellationToken ct)
        => ToActionResult(await _service.RejectAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
        => ToActionResult(await _service.ReceiveAsync(id, CurrentUserId, IsAdmin, ct));

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveReturnRequest request, CancellationToken ct)
        => ToActionResult(await _service.ResolveAsync(id, CurrentUserId, IsAdmin, request, ct));

    // ----- Admin / Finance -----

    [HttpPost("{id:guid}/complete-refund")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> CompleteRefund(Guid id, CancellationToken ct)
        => ToActionResult(await _service.CompleteRefundAsync(id, CurrentUserId, IsAdmin, ct));
}
