using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Features.Workspace.Services;

public interface IWorkspaceElementInputClassifierService
{
    /// <summary>Phân loại 1 tag mới (chưa có trong element_input_map) thành hành + weight bằng AI, đã chuẩn hóa.</summary>
    Task<IServiceResult<ClassifyElementInputResponse>> ClassifyAsync(
        ClassifyElementInputRequest request, CancellationToken ct = default);
}
