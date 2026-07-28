using System.Text.Json;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Identity.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Identity;
using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.Identity.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly IUnitOfWork _uow;
    public AdminUserService(IUnitOfWork uow) => _uow = uow;

    public async Task<IServiceResult<PagedResult<AdminUserResponse>>> GetAsync(
        AdminUserQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var (items, total) = await _uow.Users.GetAdminPageAsync(
            (page - 1) * pageSize, pageSize, query.Query, query.Role, query.IsActive, ct);
        return ServiceResult<PagedResult<AdminUserResponse>>.Success(
            new PagedResult<AdminUserResponse>(items.Select(Map).ToList(), page, pageSize, total));
    }

    public async Task<IServiceResult<AdminUserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        return user is null
            ? ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy người dùng.")
            : ServiceResult<AdminUserResponse>.Success(Map(user));
    }

    public async Task<IServiceResult<AdminUserResponse>> UpdateStatusAsync(
        Guid id, Guid actorId, string? ipAddress, UpdateUserStatusRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null)
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy người dùng.");
        if (user.Id == actorId && !request.IsActive)
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.BadRequest, "Admin không thể tự khóa tài khoản đang sử dụng.");
        if (!request.IsActive && user.Role.Has(UserRole.Admin) && await _uow.Users.CountActiveAdminsAsync(ct) <= 1)
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.Conflict, "Không thể khóa Admin hoạt động cuối cùng.");
        if (user.IsActive == request.IsActive)
            return ServiceResult<AdminUserResponse>.Success(Map(user));

        var old = new { user.IsActive, user.TokenVersion };
        user.IsActive = request.IsActive;
        user.TokenVersion++;
        await _uow.RefreshTokens.RevokeAllActiveForUserAsync(user.Id, ct);
        await AddAuditAsync(actorId, "UpdateUserStatus", user.Id, old,
            new { user.IsActive, user.TokenVersion }, request.Reason, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<AdminUserResponse>.Success(Map(user), "Đã cập nhật trạng thái người dùng.");
    }

    public async Task<IServiceResult<AdminUserResponse>> UpdateRolesAsync(
        Guid id, Guid actorId, string? ipAddress, UpdateUserRolesRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null)
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy người dùng.");

        var roles = request.Roles.Distinct().ToArray();
        if (roles.Length == 0 || roles.Any(r => r == UserRole.None || !IsSingleDefinedRole(r)))
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.BadRequest, "Danh sách role không hợp lệ.");

        var newMask = roles.Aggregate(UserRole.None, (mask, role) => mask.Add(role));
        if (user.Id == actorId && !newMask.Has(UserRole.Admin))
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.BadRequest, "Admin không thể tự gỡ quyền Admin.");
        if (user.Role.Has(UserRole.Admin) && !newMask.Has(UserRole.Admin)
            && await _uow.Users.CountActiveAdminsAsync(ct) <= 1)
            return ServiceResult<AdminUserResponse>.Failure(ApiStatusCodes.Conflict, "Không thể gỡ quyền Admin cuối cùng.");
        if (user.Role == newMask)
            return ServiceResult<AdminUserResponse>.Success(Map(user));

        var old = new { Role = user.Role.ToFlagList().Select(x => x.ToString()), user.TokenVersion };
        user.Role = newMask;
        user.TokenVersion++;
        await _uow.RefreshTokens.RevokeAllActiveForUserAsync(user.Id, ct);
        await AddAuditAsync(actorId, "UpdateUserRoles", user.Id, old,
            new { Role = user.Role.ToFlagList().Select(x => x.ToString()), user.TokenVersion },
            request.Reason, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<AdminUserResponse>.Success(Map(user), "Đã cập nhật vai trò người dùng.");
    }

    public async Task<IServiceResult> RevokeSessionsAsync(
        Guid id, Guid actorId, string? ipAddress, RevokeUserSessionsRequest request, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user is null) return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy người dùng.");
        var oldVersion = user.TokenVersion;
        user.TokenVersion++;
        await _uow.RefreshTokens.RevokeAllActiveForUserAsync(user.Id, ct);
        await AddAuditAsync(actorId, "RevokeUserSessions", user.Id,
            new { TokenVersion = oldVersion }, new { user.TokenVersion }, request.Reason, ipAddress, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã thu hồi toàn bộ phiên đăng nhập.");
    }

    public async Task<IServiceResult<PagedResult<AuthorizationAuditResponse>>> GetAuditLogsAsync(
        Guid id, PageRequest page, CancellationToken ct = default)
    {
        var (items, total) = await _uow.AuthorizationAudits.GetByResourceAsync(
            "User", id, page.Skip, page.PageSize, ct);
        var mapped = items.Select(x => new AuthorizationAuditResponse
        {
            Id = x.Id, ActorUserId = x.ActorUserId, Action = x.Action,
            ResourceType = x.ResourceType, ResourceId = x.ResourceId,
            OldValueJson = x.OldValueJson, NewValueJson = x.NewValueJson,
            Reason = x.Reason, IpAddress = x.IpAddress, CreatedAt = x.CreatedAt,
        }).ToList();
        return ServiceResult<PagedResult<AuthorizationAuditResponse>>.Success(
            new PagedResult<AuthorizationAuditResponse>(mapped, page.Page, page.PageSize, total));
    }

    private Task AddAuditAsync(Guid actorId, string action, Guid resourceId, object? oldValue,
        object? newValue, string? reason, string? ipAddress, CancellationToken ct)
        => _uow.AuthorizationAudits.AddAsync(new AuthorizationAuditLog
        {
            ActorUserId = actorId,
            Action = action,
            ResourceType = "User",
            ResourceId = resourceId,
            OldValueJson = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValueJson = newValue is null ? null : JsonSerializer.Serialize(newValue),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            IpAddress = ipAddress,
        }, ct);

    private static bool IsSingleDefinedRole(UserRole role)
        => role is UserRole.Customer or UserRole.Manager or UserRole.Staff or UserRole.Admin or UserRole.GardenOwner;

    private static AdminUserResponse Map(User user) => new()
    {
        Id = user.Id, Email = user.Email, FullName = user.FullName, Phone = user.Phone,
        Roles = user.Role.ToFlagList().Select(x => x.ToString()).ToArray(),
        IsActive = user.IsActive, TokenVersion = user.TokenVersion, CreatedAt = user.CreatedAt,
    };
}
