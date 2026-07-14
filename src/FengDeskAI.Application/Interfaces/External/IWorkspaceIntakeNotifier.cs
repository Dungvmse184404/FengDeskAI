using FengDeskAI.Application.Features.Workspace.DTOs;

namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Đẩy KẾT QUẢ intake workspace (draft / lỗi) tới client realtime — trung lập với transport (impl SignalR
/// ở WebAPI, gửi vào group "ai-op-{operationId}"). Tiến trình thinking/done dùng chung
/// <see cref="IAiActivityNotifier"/>; notifier này chỉ lo payload draft cuối cùng.
/// Best-effort: không throw, không chặn luồng chính (client có thể kết nối lại lấy từ cache).
/// </summary>
public interface IWorkspaceIntakeNotifier
{
    Task PublishResultAsync(string operationId, WorkspaceProfileDraftResponse draft, CancellationToken ct = default);

    Task PublishFailedAsync(string operationId, string message, CancellationToken ct = default);
}
