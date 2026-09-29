using FengDeskAI.Application.Features.Vendor.DTOs;
using FengDeskAI.Application.Features.Vendor.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>Chính sách chung của sàn. Xem công khai; đổi phí sàn cần Manager trở lên.</summary>
[Route("api/platform")]
public sealed class PlatformController : ApiControllerBase
{
    private readonly IPlatformFeeService _platformFee;

    public PlatformController(IPlatformFeeService platformFee) => _platformFee = platformFee;

    /// <summary>Phí sàn đang áp + khoảng giữ tiền — người bán xem trước "giá thực nhận" trước khi đăng sản phẩm.</summary>
    [HttpGet("fee-policy")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeePolicy(CancellationToken ct)
        => ToActionResult(await _platformFee.GetPolicyAsync(ct));

    /// <summary>Đổi phí sàn. Chỉ áp cho đơn đặt sau thời điểm lưu; đơn cũ giữ tỉ lệ đã chốt.</summary>
    [HttpPut("fee-policy")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> UpdateFeePolicy([FromBody] UpdatePlatformFeeRequest request, CancellationToken ct)
        => ToActionResult(await _platformFee.UpdateRateAsync(request, ct));

    /// <summary>Lịch sử thay đổi phí sàn, mới nhất trước.</summary>
    [HttpGet("fee-policy/history")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> GetFeeHistory(CancellationToken ct)
        => ToActionResult(await _platformFee.GetHistoryAsync(ct));
}
