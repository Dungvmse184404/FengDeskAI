using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Features.Chat.Services;
using FengDeskAI.Application.Interfaces.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FengDeskAI.WebAPI.Hubs;

/// <summary>SignalR hub cho chat realtime 1-1 giữa 2 người dùng.</summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ICurrentUserService _currentUser;

    public ChatHub(IChatService chatService, ICurrentUserService currentUser)
    {
        _chatService = chatService;
        _currentUser = currentUser;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = _currentUser.UserId;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            _chatService.RecordUserConnection(userId.Value, Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId.Value}");
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = _currentUser.UserId;
        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            _chatService.RemoveUserConnection(userId.Value, Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Gửi message realtime. Client gọi này, server broadcast đến chatbox group.
    /// </summary>
    public async Task SendMessage(Guid chatboxId, string content)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            await Clients.Caller.SendAsync("error", "Bạn chưa đăng nhập.");
            return;
        }

        // Việc broadcast "messageReceived" do ChatService thực hiện qua IChatRealtimeNotifier (dùng chung REST + hub).
        var result = await _chatService.SendMessageAsync(
            userId.Value, _currentUser.Role, _currentUser.Email, chatboxId,
            new SendMessageRequest { Content = content }, Context.ConnectionAborted);
        if (!result.IsSuccess)
            await Clients.Caller.SendAsync("error", result.Message);
    }

    /// <summary>Đánh dấu cả phòng đã đọc realtime.</summary>
    public async Task MarkChatboxRead(Guid chatboxId)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            await Clients.Caller.SendAsync("error", "Bạn chưa đăng nhập.");
            return;
        }

        var result = await _chatService.MarkChatboxAsReadAsync(userId.Value, chatboxId, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("error", result.Message);
            return;
        }

        await Clients.Group($"chat-{chatboxId}").SendAsync("chatboxRead", new { chatboxId, userId = userId.Value });
    }

    /// <summary>
    /// Tham gia vào chatbox group để nhận messages realtime.
    /// </summary>
    public async Task JoinChatbox(Guid chatboxId)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            await Clients.Caller.SendAsync("error", "Bạn chưa đăng nhập.");
            return;
        }

        var result = await _chatService.ValidateChatboxAccessAsync(userId.Value, chatboxId, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("error", result.Message);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat-{chatboxId}");
        // Tương thích chat cũ: aiStatus của phòng AI phát vào "ai-op-chat-{chatboxId}" (xem AiActivityNotifier).
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ai-op-chat-{chatboxId}");
        await Clients.Group($"chat-{chatboxId}").SendAsync("userJoined", new { chatboxId, userId = userId.Value });
    }

    /// <summary>Tham gia group nhận trạng thái AI realtime cho một operation (chat, workspace intake…).</summary>
    public Task JoinAiOperation(string operationId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"ai-op-{operationId}");

    /// <summary>Rời group trạng thái AI realtime.</summary>
    public Task LeaveAiOperation(string operationId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ai-op-{operationId}");

    /// <summary>
    /// Rời khỏi chatbox group.
    /// </summary>
    public async Task LeaveChatbox(Guid chatboxId)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat-{chatboxId}");
        await Clients.Group($"chat-{chatboxId}").SendAsync("userLeft", new { chatboxId, userId = userId.Value });
    }
}
