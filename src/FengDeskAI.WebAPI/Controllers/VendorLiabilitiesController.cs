using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Features.Returns.DTOs;
using FengDeskAI.Application.Features.Returns.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Công nợ vendor — cơ chế bù trừ payout SAU khi khách đã nhận tiền, KHÔNG ảnh hưởng khách.
/// Vendor xem/phản đối công nợ của garden mình; Manager phán quyết dispute.
/// </summary>
[Route("api/vendor-liabilities")]
[Authorize]
public class VendorLiabilitiesController : ApiControllerBase
{
    private readonly IVendorLiabilityService _service;

    public VendorLiabilitiesController(IVendorLiabilityService service) => _service = service;

    /// <summary>Công nợ của một garden (vendor sở hữu garden hoặc Manager).</summary>
    [HttpGet("gardens/{gardenId:guid}")]
    public async Task<IActionResult> GetByGarden(Guid gardenId, [FromQuery] PageRequest page, CancellationToken ct)
        => ToActionResult(await _service.GetByGardenAsync(gardenId, RmaActor, page, ct));

    /// <summary>Vendor phản đối khoản trừ (trong dispute_deadline).</summary>
    [HttpPost("{id:guid}/dispute")]
    [Authorize(Policy = AuthorizationPolicies.GardenOwnerOrAbove)]
    public async Task<IActionResult> Dispute(Guid id, [FromBody] VendorDisputeRequest request, CancellationToken ct)
        => ToActionResult(await _service.DisputeAsync(id, RmaActor, request, ct));

    /// <summary>Manager phán quyết dispute: vendor đúng → waived, vendor sai → settled.</summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
    public async Task<IActionResult> Resolve(Guid id, [FromBody] ResolveLiabilityRequest request, CancellationToken ct)
        => ToActionResult(await _service.ResolveAsync(id, RmaActor, request, ct));
}
