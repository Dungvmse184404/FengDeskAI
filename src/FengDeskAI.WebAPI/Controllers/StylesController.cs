using FengDeskAI.Application.Features.Catalog.DTOs;
using FengDeskAI.Application.Features.Catalog.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Bảng tra cứu phong cách (styles.code). Đọc public; thêm/sửa cho Manager trở lên — không cần deploy.</summary>
[Route("api/styles")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
public class StylesController : ApiControllerBase
{
    private readonly ITaxonomyService _service;

    public StylesController(ITaxonomyService service) => _service = service;

    /// <summary>Danh sách phong cách (mặc định chỉ active). Dùng để đổ dropdown chọn code.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => ToActionResult(await _service.GetStylesAsync(includeInactive, ct));

    /// <summary>Thêm phong cách mới.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLookupRequest request, CancellationToken ct)
        => ToActionResult(await _service.CreateStyleAsync(request, ct));

    /// <summary>Đổi tên hiển thị / bật-tắt / thứ tự (code giữ nguyên).</summary>
    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, [FromBody] UpdateLookupRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpdateStyleAsync(code, request, ct));
}
