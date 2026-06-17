using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.Workspace.Services;

public interface IWorkspaceTypeService
{
    /// <summary>Loại không gian khả dụng cho user (hệ thống + tự tạo).</summary>
    Task<IServiceResult<List<WorkspaceTypeResponse>>> GetAvailableAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Tạo loại không gian tùy chỉnh cho user.</summary>
    Task<IServiceResult<WorkspaceTypeResponse>> CreateAsync(Guid userId, CreateWorkspaceTypeRequest request, CancellationToken ct = default);
}
