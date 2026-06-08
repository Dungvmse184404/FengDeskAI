using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// CRUD workspace profile của user đang đăng nhập.
/// User chỉ thao tác được trên profile của chính mình.
/// </summary>
[Route("api/workspace")]
[Authorize]
public class WorkspaceProfilesController : ApiControllerBase
{
    private readonly IWorkspaceProfileService _service;

    public WorkspaceProfilesController(IWorkspaceProfileService service)
    {
        _service = service;
    }

    /// <summary>Danh sách workspace profile của user hiện tại.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => ToActionResult(await _service.GetMineAsync(CurrentUserId, ct));

    /// <summary>Profile mặc định (dùng làm input AI khi user không chỉ định).</summary>
    [HttpGet("default")]
    public async Task<IActionResult> GetDefault(CancellationToken ct)
        => ToActionResult(await _service.GetDefaultAsync(CurrentUserId, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, CurrentUserId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceProfileRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWorkspaceProfileRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateAsync(id, CurrentUserId, request, ct));

    /// <summary>Đặt profile làm default. Tự động bỏ default của các profile khác cùng user.</summary>
    [HttpPatch("{id:guid}/set-default")]
    public async Task<IActionResult> SetDefault(Guid id, CancellationToken ct)
        => ToActionResult(await _service.SetDefaultAsync(id, CurrentUserId, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteAsync(id, CurrentUserId, ct));
}
