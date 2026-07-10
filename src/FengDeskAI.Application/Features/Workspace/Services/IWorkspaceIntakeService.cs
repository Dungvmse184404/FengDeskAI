using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.Workspace.Services;

/// <summary>
/// Phân tích mô tả không gian bằng lời (AI) → draft prefill form. Stateless: KHÔNG bao giờ tự lưu DB —
/// user review/sửa rồi submit qua <see cref="IWorkspaceProfileService.CreateAsync"/> như bình thường.
/// </summary>
public interface IWorkspaceIntakeService
{
    Task<IServiceResult<WorkspaceProfileDraftResponse>> ParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request, CancellationToken ct = default);
}
