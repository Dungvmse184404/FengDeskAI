using FengDeskAI.Application.Interfaces.External;
using Microsoft.AspNetCore.SignalR;

namespace FengDeskAI.WebAPI.Hubs;

/// <summary>Đẩy tin chat realtime tới group SignalR "chat-{chatboxId}" (client phải JoinChatbox trước).</summary>
public sealed class ChatRealtimeNotifier : IChatRealtimeNotifier
{
    private readonly IHubContext<ChatHub> _hub;

    public ChatRealtimeNotifier(IHubContext<ChatHub> hub) => _hub = hub;

    public Task MessageReceivedAsync(ChatMessageBroadcast message, CancellationToken ct = default)
        => _hub.Clients.Group($"chat-{message.ChatboxId}").SendAsync("messageReceived", message, ct);

    public Task AiActivityAsync(Guid chatboxId, string phase, string? toolName = null, CancellationToken ct = default)
        => _hub.Clients.Group($"chat-{chatboxId}").SendAsync(
            "aiStatus", new { chatboxId, phase, toolName }, ct);
}
