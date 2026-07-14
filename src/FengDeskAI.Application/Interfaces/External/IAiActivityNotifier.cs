namespace FengDeskAI.Application.Interfaces.External;

/// <summary>
/// Trạng thái hoạt động AI realtime, trung lập với transport (impl SignalR ở WebAPI).
/// Dùng chung cho mọi khâu có AI (chat, workspace intake, recommendation explain…) — khóa định tuyến
/// là <paramref name="OperationId"/> tự do (không gắn cứng vào chatboxId).
/// </summary>
public sealed record AiActivityEvent(
    string OperationId,
    string Phase, // thinking | calling_tool | writing | narration | done | error
    string? ToolName = null,
    string? Note = null); // narration: lời dẫn trung gian của model (ephemeral, không lưu DB)

public interface IAiActivityNotifier
{
    /// <summary>Best-effort — không throw, không chặn luồng chính.</summary>
    Task PublishAsync(AiActivityEvent e, CancellationToken ct = default);

    /// <summary>Scope tự phát "done" khi dispose (kể cả khi có exception) — tránh indicator treo.</summary>
    AiActivityScope Begin(string operationId) => new(this, operationId);
}

/// <summary>Bọc một luồng xử lý AI: phát các phase trung gian, tự phát "done" khi dispose (trừ khi phase cuối là "error").</summary>
public sealed class AiActivityScope : IAsyncDisposable
{
    private readonly IAiActivityNotifier _notifier;
    private readonly string _operationId;
    private string _lastPhase = "thinking";
    private bool _disposed;

    internal AiActivityScope(IAiActivityNotifier notifier, string operationId)
    {
        _notifier = notifier;
        _operationId = operationId;
    }

    /// <paramref name="note"/>: dòng mô tả thân thiện (vd "Đang chuẩn bị đơn hàng của bạn…") hiển thị
    /// thay cho tên tool thô khi phase="calling_tool" — xem <see cref="FengDeskAI.Application.Features.CustomerCare.Services.AiChatService"/>.
    public Task PhaseAsync(string phase, string? toolName = null, string? note = null, CancellationToken ct = default)
    {
        _lastPhase = phase;
        return _notifier.PublishAsync(new AiActivityEvent(_operationId, phase, toolName, note), ct);
    }

    /// <summary>
    /// Phát lời dẫn trung gian model viết kèm tool_calls — FE hiển thị dạng "thinking",
    /// KHÔNG lưu DB (tránh phình context các lượt sau). Không đổi <c>_lastPhase</c>.
    /// </summary>
    public Task NarrateAsync(string text, CancellationToken ct = default)
        => _notifier.PublishAsync(new AiActivityEvent(_operationId, "narration", null, text), ct);

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        if (_lastPhase == "error") return ValueTask.CompletedTask;
        return new ValueTask(_notifier.PublishAsync(new AiActivityEvent(_operationId, "done")));
    }
}
