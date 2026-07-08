using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.WebAPI.Authorization;
using FengDeskAI.WebAPI.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FengDeskAI.WebAPI.Controllers;

/// <summary>
/// Quản trị cấu hình engine chấm điểm v3: tham số, map ngũ hành (màu/vật liệu/hình khối),
/// modifier theo mục đích, vector lý tưởng/nội thất theo loại phòng. Chỉ Manager trở lên.
/// </summary>
[Route("api/admin/scoring")]
[Authorize(Policy = AuthorizationPolicies.ManagerOrAbove)]
public class ScoringConfigController : ApiControllerBase
{
    private readonly IScoringConfigAdminService _service;

    public ScoringConfigController(IScoringConfigAdminService service) => _service = service;

    // ── scoring_params ──

    [HttpGet("params")]
    public async Task<IActionResult> GetParams(CancellationToken ct)
        => ToActionResult(await _service.GetParamsAsync(ct));

    [HttpPut("params/{code}")]
    public async Task<IActionResult> UpsertParam(string code, [FromBody] UpsertScoringParamRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertParamAsync(code, request, ct));

    // ── element_input_map ──

    [HttpGet("element-inputs")]
    public async Task<IActionResult> GetElementInputs(CancellationToken ct)
        => ToActionResult(await _service.GetElementInputsAsync(ct));

    [HttpPut("element-inputs")]
    public async Task<IActionResult> UpsertElementInput([FromBody] UpsertElementInputMapRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertElementInputAsync(request, ct));

    [HttpDelete("element-inputs/{id:guid}")]
    public async Task<IActionResult> DeleteElementInput(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteElementInputAsync(id, ct));

    // ── work_purpose_element_modifiers ──

    [HttpGet("purpose-modifiers")]
    public async Task<IActionResult> GetPurposeModifiers(CancellationToken ct)
        => ToActionResult(await _service.GetPurposeModifiersAsync(ct));

    [HttpPut("purpose-modifiers")]
    public async Task<IActionResult> UpsertPurposeModifier([FromBody] UpsertWorkPurposeModifierRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertPurposeModifierAsync(request, ct));

    [HttpDelete("purpose-modifiers/{id:guid}")]
    public async Task<IActionResult> DeletePurposeModifier(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeletePurposeModifierAsync(id, ct));

    // ── workspace_type_elements ──

    [HttpGet("workspace-type-elements")]
    public async Task<IActionResult> GetWorkspaceTypeElements([FromQuery] Guid? workspaceTypeId, CancellationToken ct)
        => ToActionResult(await _service.GetWorkspaceTypeElementsAsync(workspaceTypeId, ct));

    [HttpPut("workspace-type-elements")]
    public async Task<IActionResult> UpsertWorkspaceTypeElement([FromBody] UpsertWorkspaceTypeElementRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertWorkspaceTypeElementAsync(request, ct));

    [HttpDelete("workspace-type-elements/{id:guid}")]
    public async Task<IActionResult> DeleteWorkspaceTypeElement(Guid id, CancellationToken ct)
        => ToActionResult(await _service.DeleteWorkspaceTypeElementAsync(id, ct));
}
