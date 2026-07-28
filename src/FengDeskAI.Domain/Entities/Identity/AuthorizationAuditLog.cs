using FengDeskAI.Domain.Common;

namespace FengDeskAI.Domain.Entities.Identity;

/// <summary>Nhật ký bất biến cho các thao tác thay đổi quyền hoặc dữ liệu nhạy cảm.</summary>
public class AuthorizationAuditLog : BaseEntity
{
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public string ResourceType { get; set; } = null!;
    public Guid? ResourceId { get; set; }
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
    public string? Reason { get; set; }
    public string? IpAddress { get; set; }
}
