namespace FengDeskAI.Application.Interfaces.External;

/// <summary>Tin nhắn để broadcast realtime (trung lập với transport SignalR).</summary>
public sealed record ChatMessageBroadcast(
    Guid Id,
    Guid ChatboxId,
    Guid? SenderId,
    string SenderType,
    string? SenderName,
    string? Content,
    DateTime CreatedAt,
    IReadOnlyList<string> Images);

/// <summary>
/// Đẩy sự kiện chat realtime ra ngoài (impl bằng SignalR ở WebAPI). Application gọi abstraction này
/// sau khi lưu tin → không phụ thuộc SignalR. Dùng chung cho tin người gửi (REST/hub) và tin AI.
/// </summary>
public interface IChatRealtimeNotifier
{
    Task MessageReceivedAsync(ChatMessageBroadcast message, CancellationToken ct = default);

    /// <summary>
    /// Phát trạng thái trợ lý AI tới phòng (group "chat-{chatboxId}"): phase = thinking | calling_tool | writing | done.
    /// <paramref name="toolName"/> chỉ có khi phase = calling_tool. Best-effort — lỗi không chặn hội thoại.
    /// </summary>
    Task AiActivityAsync(Guid chatboxId, string phase, string? toolName = null, CancellationToken ct = default);
}
