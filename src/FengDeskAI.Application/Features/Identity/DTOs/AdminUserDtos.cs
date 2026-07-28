using FengDeskAI.Domain.Enums;

namespace FengDeskAI.Application.Features.Identity.DTOs;

public sealed class AdminUserQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Query { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool IsActive { get; set; }
    public int TokenVersion { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UpdateUserStatusRequest
{
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public sealed class UpdateUserRolesRequest
{
    public IReadOnlyCollection<UserRole> Roles { get; set; } = Array.Empty<UserRole>();
    public string? Reason { get; set; }
}

public sealed class RevokeUserSessionsRequest
{
    public string? Reason { get; set; }
}

public sealed class AuthorizationAuditResponse
{
    public Guid Id { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public string ResourceType { get; set; } = null!;
    public Guid? ResourceId { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}
