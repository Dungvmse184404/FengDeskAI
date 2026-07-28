using System.Security.Claims;
using FengDeskAI.Application.Interfaces.Security;
using FengDeskAI.WebAPI.Authorization;

namespace FengDeskAI.WebAPI.Services;

public class CurrentUserService : ICurrentUserService
{
    private static readonly string[] RolePriority =
    {
        Authorization.Roles.Admin,
        Authorization.Roles.Manager,
        Authorization.Roles.Staff,
        Authorization.Roles.GardenOwner,
        Authorization.Roles.Customer,
    };

    private readonly IHttpContextAccessor _accessor;

    public CurrentUserService(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public Guid? UserId
    {
        get
        {
            var value = _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var userId) ? userId : null;
        }
    }

    public string? Email => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);

    public string? Name => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    public IReadOnlyList<string> Roles
    {
        get
        {
            var claims = _accessor.HttpContext?.User.FindAll(ClaimTypes.Role);
            return claims is null
                ? Array.Empty<string>()
                : claims.Select(claim => claim.Value).Distinct().ToArray();
        }
    }

    public string? Role
    {
        get
        {
            var roles = Roles.ToHashSet(StringComparer.Ordinal);
            return roles.Count == 0
                ? null
                : RolePriority.FirstOrDefault(roles.Contains);
        }
    }

    public bool IsAuthenticated =>
        _accessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
