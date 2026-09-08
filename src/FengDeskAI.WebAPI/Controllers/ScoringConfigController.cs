using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.CustomerCare.Services;
using FengDeskAI.Domain.Enums.Workspace;
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

    /// <summary>Danh sách tag ngũ hành gộp theo (kind, code) — đơn vị admin thực sự chỉnh sửa.</summary>
    [HttpGet("element-input-tags")]
    public async Task<IActionResult> GetElementInputTags(
        [FromQuery] ElementInputKind? kind,
        [FromQuery] ElementInputVisibility? visibility,
        [FromQuery] bool? isUserCreated,
        CancellationToken ct)
        => ToActionResult(await _service.GetElementInputTagsAsync(kind, visibility, isUserCreated, ct));

    /// <summary>Sửa trọn 1 tag: nhãn tiếng Việt + phân bổ hành/weight + phạm vi hiển thị.</summary>
    [HttpPut("element-input-tags/{kind}/{code}")]
    public async Task<IActionResult> UpdateElementInputTag(
        ElementInputKind kind, string code,
        [FromBody] UpdateElementInputTagRequest request,
        CancellationToken ct)
        => ToActionResult(await _service.UpdateElementInputTagAsync(kind, code, request, ct));

    /// <summary>Xóa toàn bộ hành của 1 tag (dùng để dọn tag rác do user tạo).</summary>
    [HttpDelete("element-input-tags/{kind}/{code}")]
    public async Task<IActionResult> DeleteElementInputTag(
        ElementInputKind kind, string code, CancellationToken ct)
        => ToActionResult(await _service.DeleteElementInputTagAsync(kind, code, ct));

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

    // ── occupations (P5) ──

    /// <summary>Danh sách nghề kèm bảng delta — màn hình chuyên gia soát và nhập số.</summary>
    [HttpGet("occupations")]
    public async Task<IActionResult> GetOccupations([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => ToActionResult(await _service.GetOccupationsAsync(includeInactive, ct));

    /// <summary>Thêm nghề mới. Mã lấy từ body và là khoá bất biến.</summary>
    [HttpPost("occupations")]
    public async Task<IActionResult> CreateOccupation([FromBody] UpsertOccupationRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertOccupationAsync(null, request, ct));

    /// <summary>Sửa tên/mô tả/trạng thái của một nghề. KHÔNG đụng tới delta.</summary>
    [HttpPut("occupations/{code}")]
    public async Task<IActionResult> UpdateOccupation(string code, [FromBody] UpsertOccupationRequest request, CancellationToken ct)
        => ToActionResult(await _service.UpsertOccupationAsync(code, request, ct));

    /// <summary>
    /// Ghi đè TRỌN GÓI bảng delta của một nghề — đây là chỗ chuyên gia phong thủy nhập số sau khi duyệt.
    /// Seeder cố tình không seed delta: nó là phát biểu phong thủy, không phải dữ liệu tham chiếu.
    /// </summary>
    [HttpPut("occupations/{code}/modifiers")]
    public async Task<IActionResult> ReplaceOccupationModifiers(
        string code, [FromBody] ReplaceOccupationModifiersRequest request, CancellationToken ct)
        => ToActionResult(await _service.ReplaceOccupationModifiersAsync(code, request, ct));

    /// <summary>Xóa nghề. Bị chặn khi còn user đang chọn — ẩn bằng <c>isActive = false</c> thay vì xóa.</summary>
    [HttpDelete("occupations/{code}")]
    public async Task<IActionResult> DeleteOccupation(string code, CancellationToken ct)
        => ToActionResult(await _service.DeleteOccupationAsync(code, ct));

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
