using FengDeskAI.Application.Interfaces.External;
using Microsoft.AspNetCore.SignalR;

namespace FengDeskAI.WebAPI.Hubs;

/// <summary>
/// Đẩy trạng thái hoạt động AI (thinking/calling_tool/writing/done/error) tới group SignalR
/// "ai-op-{operationId}" — tái dùng <see cref="ChatHub"/> làm transport. Client phải JoinAiOperation trước.
/// Best-effort: nuốt lỗi, chỉ log debug — không được chặn luồng xử lý AI chính.
/// </summary>
public sealed class AiActivityNotifier : IAiActivityNotifier
{
    private readonly IHubContext<ChatHub> _hub;
    private readonly ILogger<AiActivityNotifier> _logger;

    public AiActivityNotifier(IHubContext<ChatHub> hub, ILogger<AiActivityNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PublishAsync(AiActivityEvent e, CancellationToken ct = default)
    {
        try
        {
            await _hub.Clients.Group($"ai-op-{e.OperationId}").SendAsync("aiStatus", e, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[AiActivity] Publish phase {Phase} cho {OperationId} lỗi (bỏ qua).", e.Phase, e.OperationId);
        }
    }
}
