using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Loại không gian làm việc — quyết định trọng số cá nhân khi gợi ý.
/// User thấy loại hệ thống + loại mình tạo; tạo mới mặc định trọng số 1.0.
/// </summary>
[Route("api/workspace-types")]
[Authorize]
public class WorkspaceTypesController : ApiControllerBase
{
    private readonly IWorkspaceTypeService _service;

    public WorkspaceTypesController(IWorkspaceTypeService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAvailable(CancellationToken ct)
        => ToActionResult(await _service.GetAvailableAsync(CurrentUserId, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkspaceTypeRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateAsync(CurrentUserId, request, ct));
}
