using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Features.Workspace.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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
    private readonly IWorkspaceIntakeService _intakeService;

    public WorkspaceProfilesController(IWorkspaceProfileService service, IWorkspaceIntakeService intakeService)
    {
        _service = service;
        _intakeService = intakeService;
    }

    /// <summary>
    /// AI intake: mô tả không gian bằng lời → draft prefill form. Stateless — KHÔNG lưu DB;
    /// user review/sửa rồi submit qua <see cref="Create"/> như bình thường.
    /// </summary>
    [HttpPost("parse-description")]
    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [EnableRateLimiting("workspace-intake")]
    public async Task<IActionResult> ParseDescription(
        [FromBody] ParseWorkspaceDescriptionRequest request, CancellationToken ct)
        => ToActionResult(await _intakeService.ParseAsync(CurrentUserId, request, ct));

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

    /// <summary>Phân tích ngũ hành phòng (ideal/adjustedIdeal/current/gap) — FE hiển thị phòng thiếu/thừa hành gì.</summary>
    [HttpGet("{id:guid}/element-analysis")]
    public async Task<IActionResult> GetElementAnalysis(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetElementAnalysisAsync(id, CurrentUserId, ct));

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
