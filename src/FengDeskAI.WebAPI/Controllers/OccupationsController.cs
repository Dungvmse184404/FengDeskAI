using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Bảng tra cứu nghề nghiệp cho màn hình hồ sơ. Chỉ đọc và không kèm delta — thêm/sửa/nhập delta nằm
/// ở <c>/api/admin/scoring/occupations</c> (Manager trở lên).
/// </summary>
[Route("api/occupations")]
[AllowAnonymous]
public class OccupationsController : ApiControllerBase
{
    private readonly IOccupationService _service;

    public OccupationsController(IOccupationService service) => _service = service;

    /// <summary>Danh sách nghề đang bật, sắp theo <c>sortOrder</c>.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => ToActionResult(await _service.GetOptionsAsync(ct));
}
