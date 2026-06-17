using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Bảng tra cứu vibe (vibes.code). Đọc public; thêm/sửa cho Manager trở lên — không cần deploy.</summary>
[Route("api/vibes")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
public class VibesController : ApiControllerBase
{
    private readonly ITaxonomyService _service;

    public VibesController(ITaxonomyService service) => _service = service;

    /// <summary>Danh sách vibe (mặc định chỉ active).</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => ToActionResult(await _service.GetVibesAsync(includeInactive, ct));

    /// <summary>Thêm vibe mới.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLookupRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateVibeAsync(request, ct));

    /// <summary>Đổi tên hiển thị / bật-tắt / thứ tự (code giữ nguyên).</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateLookupRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateVibeAsync(code, request, ct));
}
