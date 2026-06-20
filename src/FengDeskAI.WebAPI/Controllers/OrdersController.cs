using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Sales.DTOs;
using FengDeskAI.Application.Features.Sales.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Đơn hàng. Customer: checkout từ giỏ, xem đơn của mình, hủy đơn.
/// Vendor (owner/staff store): xem & cập nhật trạng thái các delivery của store mình.
/// </summary>
[Route("api/orders")]
[Authorize]
public class OrdersController : ApiControllerBase
{
    private readonly IOrderService _service;

    public OrdersController(IOrderService service) => _service = service;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [HttpPost]
    public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request, CancellationToken ct)
        => ToActionResult(await _service.CheckoutAsync(CurrentUserId, request, ct));

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, page, ct));

    /// <summary>Tất cả đơn của mọi customer (paged) — chỉ admin.</summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> GetAll([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetAllAsync(page, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, CurrentUserId, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
        => ToActionResult(await _service.CancelAsync(id, CurrentUserId, ct));

    /// <summary>Danh sách delivery của một store (màn vendor). Yêu cầu owner/staff store đó hoặc admin.</summary>
    [HttpGet("stores/{storeId:guid}/deliveries")]
    public async Task<IActionResult> GetStoreDeliveries(Guid storeId, [FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetStoreDeliveriesAsync(storeId, CurrentUserId, IsAdmin, page, ct));

    [HttpPatch("deliveries/{deliveryId:guid}/status")]
    public async Task<IActionResult> UpdateDeliveryStatus(Guid deliveryId, [FromBody] UpdateDeliveryStatusRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateDeliveryStatusAsync(deliveryId, CurrentUserId, IsAdmin, request, ct));
}
