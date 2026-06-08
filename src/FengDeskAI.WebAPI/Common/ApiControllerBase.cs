using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Common;

/// <summary>
/// Base cho mọi API controller — gom helper truy cập user hiện tại + chuyển ServiceResult thành IActionResult.
/// Sub-class chỉ cần khai báo [Route] và (tùy chọn) [Authorize].
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    private ICurrentUserService? _currentUserCache;

    /// <summary>
    /// Service đọc thông tin user từ HttpContext (qua JWT claims).
    /// Lazy resolve để sub-class khỏi phải inject ICurrentUserService ở constructor.
    /// </summary>
    protected ICurrentUserService CurrentUser
        => _currentUserCache ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

    /// <summary>
    /// UserId của user đang authenticate.
    /// Yêu cầu action/controller có <c>[Authorize]</c>; nếu không sẽ throw
    /// <see cref="UnauthorizedAccessException"/> → filter trả 401.
    /// Cho endpoint <c>[AllowAnonymous]</c> mà vẫn cần userId (optional), dùng <c>CurrentUser.UserId</c>.
    /// </summary>
    protected Guid CurrentUserId
        => CurrentUser.UserId
           ?? throw new UnauthorizedAccessException("Token không hợp lệ hoặc thiếu user identity claim.");

    /// <summary>Chuyển ServiceResult (không có data) thành IActionResult với đúng status code.</summary>
    protected IActionResult ToActionResult(IServiceResult result)
        => StatusCode(result.StatusCode, result);

    /// <summary>Chuyển ServiceResult&lt;T&gt; (có data) thành IActionResult với đúng status code.</summary>
    protected IActionResult ToActionResult<T>(IServiceResult<T> result)
        => StatusCode(result.StatusCode, result);
}
