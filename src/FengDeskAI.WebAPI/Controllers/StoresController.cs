using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Quản lý garden store (marketplace). Public xem danh sách/chi tiết;
/// user đã đăng nhập tự mở store (self-service, thành owner chính);
/// owner/admin sửa store, địa chỉ, đồng sở hữu, phân công nhân viên.
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

    /// <summary>Các store mà user hiện tại đồng sở hữu (kênh người bán). Yêu cầu đăng nhập.</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, ct));

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, ct));

    /// <summary>Vai trò của user hiện tại với store (owner chính / đồng sở hữu / staff) — FE ẩn/hiện tab theo đây.</summary>
    [HttpGet("{id:guid}/membership")]
    public async Task<IActionResult> GetMyMembership(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetMyMembershipAsync(id, CurrentUserId, IsAdmin, ct));

    /// <summary>Thống kê dashboard vendor (doanh thu, đơn theo trạng thái…). Chỉ owner/admin — staff bị 403.</summary>
    [HttpGet("{id:guid}/statistics")]
    public async Task<IActionResult> GetStatistics(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetStatisticsAsync(id, CurrentUserId, IsAdmin, ct));

    /// <summary>Tự mở store (self-service). Người tạo trở thành owner chính + được cấp role GardenOwner.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStoreRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAsync(id, CurrentUserId, IsAdmin, ct));

    /// <summary>Xóa vĩnh viễn store (vật lý). Chỉ Staff hoặc Admin.</summary>
    [HttpDelete("{id:guid}/hard")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.HardDeleteAsync(id, ct));

    // ===== Địa chỉ store (1-1) =====

    [HttpPost("{id:guid}/address")]
    public async Task<IActionResult> AddAddress(Guid id, [FromBody] CreateStoreAddressRequest request, CancellationToken ct)
        => ToActionResult(await _service.AddAddressAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpPut("{id:guid}/address")]
    public async Task<IActionResult> UpdateAddress(Guid id, [FromBody] UpdateStoreAddressRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAddressAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}/address")]
    public async Task<IActionResult> DeleteAddress(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAddressAsync(id, CurrentUserId, IsAdmin, ct));

    /// <summary>Xóa vĩnh viễn địa chỉ store (vật lý). Chỉ Staff hoặc Admin.</summary>
    [HttpDelete("{id:guid}/address/hard")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public async Task<IActionResult> HardDeleteAddress(Guid id, CancellationToken ct)
        => ToActionResult(await _service.HardDeleteAddressAsync(id, ct));

    // ===== Đồng sở hữu (owners) =====

    [HttpGet("{id:guid}/owners")]
    public async Task<IActionResult> GetOwners(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetOwnersAsync(id, ct));

    /// <summary>Thêm đồng sở hữu (chỉ owner hiện tại hoặc Admin).</summary>
    [HttpPost("{id:guid}/owners")]
    public async Task<IActionResult> AddOwner(Guid id, [FromBody] AddOwnerRequest request, CancellationToken ct)
        => ToActionResult(await _service.AddOwnerAsync(id, CurrentUserId, IsAdmin, request, ct));

    /// <summary>Gỡ đồng sở hữu (chỉ owner hiện tại hoặc Admin; không gỡ owner chính).</summary>
    [HttpDelete("{id:guid}/owners/{userId:guid}")]
    public async Task<IActionResult> RemoveOwner(Guid id, Guid userId, CancellationToken ct)
        => ToActionResult(await _service.RemoveOwnerAsync(id, userId, CurrentUserId, IsAdmin, ct));

    [HttpGet("{id:guid}/staff")]
    public async Task<IActionResult> GetStaff(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetStaffAsync(id, CurrentUserId, IsAdmin, ct));

    [HttpPost("{id:guid}/staff")]
    public async Task<IActionResult> AssignStaff(Guid id, [FromBody] AssignStaffRequest request, CancellationToken ct)
        => ToActionResult(await _service.AssignStaffAsync(id, CurrentUserId, IsAdmin, request, ct));

    [HttpDelete("{id:guid}/staff/{assignmentId:guid}")]
    public async Task<IActionResult> UnassignStaff(Guid id, Guid assignmentId, CancellationToken ct)
        => ToActionResult(await _service.UnassignStaffAsync(id, assignmentId, CurrentUserId, IsAdmin, ct));

    // ===== Invitation (góc nhìn người được mời) =====

    /// <summary>Các lời mời Pending gửi cho user hiện tại — dùng cho MyInvitationsPage.</summary>
    [HttpGet("staff/invitations/mine")]
    public async Task<IActionResult> GetMyInvitations(CancellationToken ct)
        => ToActionResult(await _service.GetMyInvitationsAsync(CurrentUserId, ct));

    /// <summary>Đồng ý lời mời — chỉ chính người được mời và khi assignment đang Pending.</summary>
    [HttpPost("staff/{assignmentId:guid}/accept")]
    public async Task<IActionResult> AcceptInvitation(Guid assignmentId, CancellationToken ct)
        => ToActionResult(await _service.AcceptInvitationAsync(assignmentId, CurrentUserId, ct));

    /// <summary>Từ chối lời mời — chỉ chính người được mời và khi assignment đang Pending.</summary>
    [HttpPost("staff/{assignmentId:guid}/reject")]
    public async Task<IActionResult> RejectInvitation(Guid assignmentId, CancellationToken ct)
        => ToActionResult(await _service.RejectInvitationAsync(assignmentId, CurrentUserId, ct));
}
