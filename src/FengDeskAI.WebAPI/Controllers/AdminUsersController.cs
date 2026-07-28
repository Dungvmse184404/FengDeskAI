using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

[Route("api/admin/users")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class AdminUsersController : ApiControllerBase
{
    private readonly IAdminUserService _service;
    public AdminUsersController(IAdminUserService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AdminUserQuery query, CancellationToken ct)
        => ToActionResult(await _service.GetAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
        => ToActionResult(await _service.GetByIdAsync(id, ct));

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateStatusAsync(id, CurrentUserId, ClientIp, request, ct));

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> UpdateRoles(
        Guid id, [FromBody] UpdateUserRolesRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateRolesAsync(id, CurrentUserId, ClientIp, request, ct));

    [HttpPost("{id:guid}/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(
        Guid id, [FromBody] RevokeUserSessionsRequest? request, CancellationToken ct)
        => ToActionResult(await _service.RevokeSessionsAsync(
            id, CurrentUserId, ClientIp, request ?? new RevokeUserSessionsRequest(), ct));

    [HttpGet("{id:guid}/audit-logs")]
    public async Task<IActionResult> GetAuditLogs(Guid id, [FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetAuditLogsAsync(id, page, ct));

    private string? ClientIp => HttpContext.Connection.RemoteIpAddress?.ToString();
}
