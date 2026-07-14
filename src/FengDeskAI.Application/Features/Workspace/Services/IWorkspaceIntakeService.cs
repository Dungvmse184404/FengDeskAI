using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Features.Workspace.Services;

/// <summary>
/// Phân tích mô tả không gian bằng lời (AI) → draft prefill form. Stateless: KHÔNG bao giờ tự lưu DB —
/// user review/sửa rồi submit qua <see cref="IWorkspaceProfileService.CreateAsync"/> như bình thường.
/// </summary>
public interface IWorkspaceIntakeService
{
    Task<IServiceResult<WorkspaceProfileDraftResponse>> ParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request,
        IProgress<AiStreamChunk>? onDelta = null, CancellationToken ct = default);

    /// <summary>
    /// Nhận yêu cầu intake, validate nhanh (đồng bộ), rồi ĐẨY job vào hàng đợi nền → trả operationId ngay
    /// (KHÔNG chờ LLM). FE dùng operationId để nghe realtime + poll fallback.
    /// </summary>
    Task<IServiceResult<WorkspaceIntakeStartResponse>> StartParseAsync(
        Guid userId, ParseWorkspaceDescriptionRequest request, CancellationToken ct = default);

    /// <summary>Worker nền gọi: chạy parse (LLM), cache kết quả + push realtime. Nuốt lỗi (đã cache trạng thái).</summary>
    Task RunJobAsync(WorkspaceIntakeJob job, CancellationToken ct = default);

    /// <summary>Trạng thái job (pending/done/failed) từ cache — null nếu operationId không tồn tại/đã hết hạn.</summary>
    IServiceResult<WorkspaceIntakeJobStatusResponse> GetJobStatus(string operationId);

    /// <summary>Tải 1 ảnh không gian lên storage (KHÔNG gắn với entity nào — chỉ để feed AI phân tích).</summary>
    Task<IServiceResult<string>> UploadImageAsync(
        Guid userId, Stream content, string fileName, string contentType, CancellationToken ct = default);
}
