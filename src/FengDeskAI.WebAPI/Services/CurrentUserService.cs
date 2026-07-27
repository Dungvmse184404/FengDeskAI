using System.Security.Claims;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.WebAPI.Authorization;

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

    public IReadOnlyList<string> Roles
    {
        get
        {
            var claims = _accessor.HttpContext?.User.FindAll(ClaimTypes.Role);
            return claims is null ? Array.Empty<string>() : claims.Select(c => c.Value).Distinct().ToArray();
        }
    }

    public string? Role
    {
        get
        {
            var roles = Roles;
            if (roles.Count == 0)
                return null;

            if (roles.Contains(FengDeskAI.WebAPI.Authorization.Roles.Admin))
                return FengDeskAI.WebAPI.Authorization.Roles.Admin;
            if (roles.Contains(FengDeskAI.WebAPI.Authorization.Roles.Manager))
                return FengDeskAI.WebAPI.Authorization.Roles.Manager;
            if (roles.Contains(FengDeskAI.WebAPI.Authorization.Roles.Staff))
                return FengDeskAI.WebAPI.Authorization.Roles.Staff;
            if (roles.Contains(FengDeskAI.WebAPI.Authorization.Roles.GardenOwner))
                return FengDeskAI.WebAPI.Authorization.Roles.GardenOwner;

            return roles[0];
        }
    }

    public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;
}
