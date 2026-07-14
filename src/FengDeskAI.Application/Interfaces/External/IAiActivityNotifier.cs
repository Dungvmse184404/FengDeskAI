using FengDeskAI.Application.Common;

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

    /// <summary>
    /// Sink nhận delta stream từ model → gom (coalesce) rồi phát phase="thinking" kèm ĐUÔI chuỗi suy luận
    /// (để FE hiện "1 dòng chữ chạy"). Truyền vào <c>IAiChatClient.CompleteAsync(onDelta:)</c>.
    /// </summary>
    public IProgress<AiStreamChunk> ThinkingProgress() => new AiThinkingProgress(this);
}

/// <summary>
/// Gom delta thinking (chống flood SignalR): cứ ~120ms HOẶC mỗi ~40 ký tự mới thì phát 1 lần, gửi phần
/// ĐUÔI (tối đa ~180 ký tự) để hợp 1 dòng chạy. Fire-and-forget, best-effort — không chặn luồng model.
/// Bỏ qua delta "content" (ở đây chỉ lo thinking; muốn stream đáp án thì xử lý riêng).
/// </summary>
internal sealed class AiThinkingProgress : IProgress<AiStreamChunk>
{
    private const int FlushChars = 40;
    private const int TailChars = 180;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(120);

    private readonly AiActivityScope _scope;
    private readonly System.Text.StringBuilder _buffer = new();
    private readonly object _lock = new();
    private DateTime _lastFlush = DateTime.MinValue;
    private int _sinceFlush;

    public AiThinkingProgress(AiActivityScope scope) => _scope = scope;

    public void Report(AiStreamChunk chunk)
    {
        if (chunk.Kind != AiStreamKind.Thinking || string.IsNullOrEmpty(chunk.Text)) return;

        string? tail = null;
        lock (_lock)
        {
            _buffer.Append(chunk.Text);
            _sinceFlush += chunk.Text.Length;
            var now = DateTime.UtcNow;
            if (_sinceFlush >= FlushChars || now - _lastFlush >= FlushInterval)
            {
                _lastFlush = now;
                _sinceFlush = 0;
                var s = _buffer.ToString();
                tail = s.Length > TailChars ? s[^TailChars..] : s;
            }
        }
        if (tail is not null) _ = SafePublishAsync(tail);
    }

    private async Task SafePublishAsync(string tail)
    {
        // Che GUID trần model lỡ "nghĩ" ra (vd lý giải về tool result chứa ID) trước khi lên UI.
        try { await _scope.PhaseAsync("thinking", note: AiTextSanitizer.CensorEntityIds(tail)); }
        catch { /* best-effort: lỗi realtime không được chặn model */ }
    }
}
