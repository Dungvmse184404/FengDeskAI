using FengDeskAI.Application.Features.Identity.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Tra cứu user công khai — phục vụ luồng "mời nhân viên" của garden owner.
/// Chỉ trả field tối thiểu, không lộ PII nhạy cảm (DOB, balance, role…).
/// </summary>
[Route("api/users")]
[Authorize(Policy = AuthorizationPolicies.GardenOwnerOrAbove)]
public class UsersController : ApiControllerBase
{
    private readonly IUserService _service;

    public UsersController(IUserService service) => _service = service;

    /// <summary>Tìm user kiểu GitHub: q match email / fullName (có dấu) / phone. Yêu cầu ≥ 3 ký tự.</summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, [FromQuery] int? limit, CancellationToken ct)
        => ToActionResult(await _service.SearchAsync(q, limit, ct));
}
