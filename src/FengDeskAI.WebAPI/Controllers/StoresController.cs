using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Quản lý garden store (multi-vendor). Public xem danh sách/chi tiết;
/// Admin tạo store + gán owner; owner/admin sửa store, địa chỉ, phân công nhân viên.
/// </summary>
[Route("api/stores")]
[Authorize]
public class StoresController : ApiControllerBase
{
    private readonly IStoreService _service;

    public StoresController(IStoreService service) => _service = service;

    private bool IsAdmin => User.IsInRole(Roles.Admin);

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActive(CancellationToken ct)
        => ToActionResult(await _service.GetActiveAsync(ct));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStoreRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPut("{id:guid}/address")]
    public async Task<IActionResult> UpsertAddress(Guid id, [FromBody] UpsertStoreAddressRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertAddressAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpGet("{id:guid}/staff")]
    public async Task<IActionResult> GetStaff(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetStaffAsync(id, CurrentUserId, IsAdmin, ct));

    [HttpPost("{id:guid}/staff")]
    public async Task<IActionResult> AssignStaff(Guid id, [FromBody] AssignStaffRequest request, CancellationToken ct)
        => ToActionResult(await _service.AssignStaffAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}/staff/{assignmentId:guid}")]
    public async Task<IActionResult> UnassignStaff(Guid id, Guid assignmentId, CancellationToken ct)
        => ToActionResult(await _service.UnassignStaffAsync(id, assignmentId, CurrentUserId, IsAdmin, ct));
}
