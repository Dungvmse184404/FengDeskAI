using System.Linq;
using System.Security.Claims;
using FengDeskAI.Application.Interfaces.Security;

namespace FengDeskAI.WebAPI.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    public string? Email => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? Name => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    // Một user có thể mang NHIỀU role (UserRole là [Flags]). Token ghi mỗi flag một claim ClaimTypes.Role,
    // và Customer nằm đầu enum → nếu lấy claim đầu sẽ luôn ra "Customer" cho cả staff/manager.
    // Vì vậy chọn role CAO NHẤT theo thứ tự ưu tiên để suy đúng ParticipantType/sender_type.
    private static readonly string[] RolePriority = { "Admin", "Manager", "Staff", "GardenOwner", "Customer" };

    public string? Role
    {
        get
        {
            var roles = _accessor.HttpContext?.User
                .FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();
            if (roles is null || roles.Count == 0) return null;
            return RolePriority.FirstOrDefault(roles.Contains);
        }
    }

    public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
