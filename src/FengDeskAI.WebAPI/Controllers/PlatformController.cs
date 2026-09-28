using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Thông tin chính sách chung của sàn, công khai.</summary>
[Route("api/platform")]
public sealed class PlatformController : ApiControllerBase
{
    /// <summary>Phí sàn + khoảng giữ tiền — người bán xem trước "giá thực nhận" trước khi đăng sản phẩm.</summary>
    [HttpGet("fee-policy")]
    [AllowAnonymous]
    public IActionResult GetFeePolicy()
        => ToActionResult(ServiceResult<PlatformFeePolicyResponse>.Success(PlatformFeePolicy.Describe()));
}
