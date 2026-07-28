using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;

namespace FengDeskAI.Application.Features.Identity.Services;

public interface IAdminUserService
{
    Task<IServiceResult<PagedResult<AdminUserResponse>>> GetAsync(AdminUserQuery query, CancellationToken ct = default);
    Task<IServiceResult<AdminUserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IServiceResult<AdminUserResponse>> UpdateStatusAsync(
        Guid id, Guid actorId, string? ipAddress, UpdateUserStatusRequest request, CancellationToken ct = default);
    Task<IServiceResult<AdminUserResponse>> UpdateRolesAsync(
        Guid id, Guid actorId, string? ipAddress, UpdateUserRolesRequest request, CancellationToken ct = default);
    Task<IServiceResult> RevokeSessionsAsync(
        Guid id, Guid actorId, string? ipAddress, RevokeUserSessionsRequest request, CancellationToken ct = default);
    Task<IServiceResult<PagedResult<AuthorizationAuditResponse>>> GetAuditLogsAsync(
        Guid id, PageRequest page, CancellationToken ct = default);
}
