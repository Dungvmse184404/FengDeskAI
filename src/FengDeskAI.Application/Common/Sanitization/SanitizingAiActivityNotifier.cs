using FengDeskAI.Application.Interfaces.External;

namespace FengDeskAI.Application.Common.Sanitization;

/// <summary>
/// Decorator bọc notifier thật: MỌI <see cref="AiActivityEvent"/> đẩy realtime đều bị lọc ở đây,
/// bất kể ai publish (thinking tail, narration, nhãn tool, workspace intake…). Đặt bộ lọc tại một
/// choke point duy nhất thay vì rải rác ở từng call site — thêm luồng AI mới là tự động được che.
/// </summary>
public sealed class SanitizingAiActivityNotifier : IAiActivityNotifier
{
    private readonly IAiActivityNotifier _inner;
    private readonly IAiTextSanitizer _sanitizer;

    public SanitizingAiActivityNotifier(IAiActivityNotifier inner, IAiTextSanitizer sanitizer)
    {
        _inner = inner;
        _sanitizer = sanitizer;
    }

    public Task PublishAsync(AiActivityEvent e, CancellationToken ct = default)
    {
        var note = e.Note is null ? null : _sanitizer.Sanitize(e.Note, SanitizeMode.LiveStream);
        // ToolName là tên hàm nội bộ, FE không render — không đẩy ra dây cho khỏi lộ qua DevTools.
        // Nhãn thân thiện đã nằm ở Note (xem AiChatService.ToolFriendlyNotes).
        return _inner.PublishAsync(e with { ToolName = null, Note = note }, ct);
    }
}
