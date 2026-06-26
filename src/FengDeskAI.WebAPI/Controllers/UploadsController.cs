using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Storage.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Upload ảnh dùng chung (multipart/form-data, field "file"). Trả URL công khai để FE đính vào entity
/// sau (vd ảnh sản phẩm lúc tạo — khi chưa có productId). Yêu cầu đăng nhập.
/// </summary>
[Route("api/uploads")]
[Authorize]
public class UploadsController : ApiControllerBase
{
    private readonly IUploadService _service;

    public UploadsController(IUploadService service) => _service = service;

    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return ToActionResult(ServiceResult<string>.Failure(ApiStatusCodes.BadRequest, "Thiếu tệp ảnh."));

        await using var stream = file.OpenReadStream();
        return ToActionResult(await _service.UploadImageAsync(stream, file.FileName, file.ContentType, ct));
    }
}
