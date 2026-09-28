using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Promotion.DTOs;
using FengDeskAI.Application.Features.Promotion.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Mã giảm giá. Khách chỉ xem mã đang áp dụng — việc áp mã nằm ở checkout (<c>voucherCode</c>) để số xem trước
/// và số bị tính luôn đi qua cùng một chỗ. Manager tạo/bật/tắt mã.
/// </summary>
[Route("api/vouchers")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
public class VouchersController : ApiControllerBase
{
    private readonly IVoucherService _service;

    public VouchersController(IVoucherService service) => _service = service;

    /// <summary>Mã đang bật và còn hạn — hiện ở giỏ/checkout.</summary>
    [HttpGet("available")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAvailable(CancellationToken ct)
        => ToActionResult(await _service.GetAvailableAsync(ct));

    [HttpGet]
    public async Task<IActionResult> GetPaged([FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetPagedAsync(page, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVoucherRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(request, ct));

    [HttpPatch("{id:guid}/active")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetVoucherActiveRequest request, CancellationToken ct)
        => ToActionResult(await _service.SetActiveAsync(id, request.IsActive, ct));
}
