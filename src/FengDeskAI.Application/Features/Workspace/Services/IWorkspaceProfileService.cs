using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.CustomerCare.DTOs;
using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.Workspace.Services;

public interface IWorkspaceProfileService
{
    Task<IServiceResult<List<WorkspaceProfileResponse>>> GetMineAsync(Guid userId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Vector ngũ hành (ideal/adjustedIdeal/current/gap) của workspace — không chạy phiên recommendation.</summary>
    Task<IServiceResult<WorkspaceElementAnalysisResponse>> GetElementAnalysisAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> GetDefaultAsync(Guid userId, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> CreateAsync(Guid userId, CreateWorkspaceProfileRequest request, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> UpdateAsync(Guid id, Guid userId, UpdateWorkspaceProfileRequest request, CancellationToken ct = default);
    Task<IServiceResult<WorkspaceProfileResponse>> SetDefaultAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IServiceResult> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
