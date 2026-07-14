using FengDeskAI.Application.Features.Workspace.DTOs;
using FengDeskAI.Application.Interfaces.External;
using Microsoft.AspNetCore.SignalR;

namespace FengDeskAI.WebAPI.Hubs;

/// <summary>
/// Đẩy KẾT QUẢ intake workspace tới group SignalR "ai-op-{operationId}" — tái dùng <see cref="ChatHub"/>
/// làm transport (client JoinAiOperation trước). Best-effort: nuốt lỗi, chỉ log debug — client vẫn có thể
/// poll GET parse-description/{operationId} lấy từ cache nếu lỡ mất event.
/// </summary>
public sealed class WorkspaceIntakeNotifier : IWorkspaceIntakeNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<WorkspaceIntakeNotifier> _logger;

    public WorkspaceIntakeNotifier(IHubContext<ChatHub> hub, ILogger<WorkspaceIntakeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishResultAsync(string operationId, WorkspaceProfileDraftResponse draft, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group($"ai-op-{operationId}").SendAsync("workspaceIntakeResult", draft, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[WorkspaceIntake] Push result cho {OperationId} lỗi (bỏ qua).", operationId);
        }
    }

    public async Task PublishFailedAsync(string operationId, string message, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group($"ai-op-{operationId}").SendAsync("workspaceIntakeFailed", new { message }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[WorkspaceIntake] Push failed cho {OperationId} lỗi (bỏ qua).", operationId);
        }
    }
}
