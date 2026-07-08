using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Domain.Enums.Workspace;

namespace FengDeskAI.Application.Features.CustomerCare.Services;

/// <summary>
/// Admin CRUD cấu hình engine chấm điểm v3 (scoring_params, element_input_map,
/// work_purpose_element_modifiers, workspace_type_elements). Quyền: ManagerOrAbove.
/// </summary>
public interface IScoringConfigAdminService
{
    // scoring_params
    Task<IServiceResult<List<ScoringParamDto>>> GetParamsAsync(CancellationToken ct = default);
    Task<IServiceResult<ScoringParamDto>> UpsertParamAsync(string code, UpsertScoringParamRequest request, CancellationToken ct = default);

    // element_input_map
    Task<IServiceResult<List<ElementInputMapDto>>> GetElementInputsAsync(CancellationToken ct = default);
    Task<IServiceResult<ElementInputMapDto>> UpsertElementInputAsync(UpsertElementInputMapRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeleteElementInputAsync(Guid id, CancellationToken ct = default);

    // work_purpose_element_modifiers
    Task<IServiceResult<List<WorkPurposeModifierDto>>> GetPurposeModifiersAsync(CancellationToken ct = default);
    Task<IServiceResult<WorkPurposeModifierDto>> UpsertPurposeModifierAsync(UpsertWorkPurposeModifierRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeletePurposeModifierAsync(Guid id, CancellationToken ct = default);

    // workspace_type_elements
    Task<IServiceResult<List<WorkspaceTypeElementDto>>> GetWorkspaceTypeElementsAsync(Guid? workspaceTypeId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceTypeElementDto>> UpsertWorkspaceTypeElementAsync(UpsertWorkspaceTypeElementRequest request, CancellationToken ct = default);
    Task<IServiceResult> DeleteWorkspaceTypeElementAsync(Guid id, CancellationToken ct = default);
}
