using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Bảng tra cứu ngũ hành (elements.code). Đọc public; sửa tên hiển thị cho Manager trở lên.
/// KHÔNG thêm hành mới (ngũ hành cố định 5) và KHÔNG đổi code (engine tham chiếu).
/// </summary>
[Route("api/elements")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
public class ElementsController : ApiControllerBase
{
    private readonly ITaxonomyService _service;

    public ElementsController(ITaxonomyService service) => _service = service;

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => ToActionResult(await _service.GetElementsAsync(includeInactive, ct));

    /// <summary>Đổi tên hiển thị / bật-tắt / thứ tự (code giữ nguyên).</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateLookupRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateElementAsync(code, request, ct));
}
