using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>[CHỈ DEVELOPMENT] Giả lập callback refund để FE test trọn luồng khi PayOS chưa có refund API thật.</summary>
[Route("api/dev/refunds")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class DevRefundsController : ApiControllerBase
{
    private readonly IRefundService _service;
    private readonly IWebHostEnvironment _env;

    public DevRefundsController(IRefundService service, IWebHostEnvironment env)
    {
        _service = service;
        _env = env;
    }

    [HttpPost("{refundId:guid}/success")]
    public async Task<IActionResult> SimulateSuccess(Guid refundId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _service.SimulateResultAsync(refundId, true, RmaActor, ct));
    }

    [HttpPost("{refundId:guid}/failed")]
    public async Task<IActionResult> SimulateFailed(Guid refundId, CancellationToken ct)
    {
        if (!_env.IsDevelopment()) return NotFound();
        return ToActionResult(await _service.SimulateResultAsync(refundId, false, RmaActor, ct));
    }
}
