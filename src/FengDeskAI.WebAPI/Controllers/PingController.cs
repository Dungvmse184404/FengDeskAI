using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Các endpoint demo để test authorization policies.
/// Có thể xóa/đổi sau khi auth đã verify hoạt động.
/// </summary>
[Area("dev")]
[Route("api/[area]/ping")]
public class PingController : ApiControllerBase
{
    /// <summary>Không cần auth — sanity check service.</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public() => Ok(new { ok = true, who = "anonymous" });

    /// <summary>Chỉ cần login (bất kỳ role nào).</summary>
    [HttpGet("authenticated")]
    [Authorize]
    public IActionResult Authenticated() => Ok(new
    {
        ok = true,
        userId = CurrentUserId,
        email = CurrentUser.Email,
    });

    /// <summary>Chỉ Admin được vào.</summary>
    [HttpGet("admin")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public IActionResult AdminOnly() => Ok(new { ok = true, who = "admin" });

    /// <summary>Manager trở lên (Manager, Staff, Admin).</summary>
    [HttpGet("manager")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public IActionResult ManagerOrAbove() => Ok(new { ok = true, who = "manager_or_above" });

    /// <summary>Staff trở lên (Staff, Admin).</summary>
    [HttpGet("staff")]
    [Authorize(Policy = AuthorizationPolicies.StaffOrAbove)]
    public IActionResult StaffOrAbove() => Ok(new { ok = true, who = "staff_or_above" });
}
