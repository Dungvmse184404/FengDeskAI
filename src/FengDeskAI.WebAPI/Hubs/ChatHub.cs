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

        var result = await _chatService.SendMessageAsync(userId.Value, chatboxId, new SendMessageRequest { Content = content }, Context.ConnectionAborted);
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("error", result.Message);
            return;
        }

        // Broadcast message đến tất cả clients trong chatbox
        var message = result.Data;
        if (message is not null)
        {
            await Clients.Group($"chat-{chatboxId}").SendAsync("messageReceived", new
            {
                message.Id,
                message.ChatboxId,
                message.SenderUserId,
                message.Content,
                message.IsRead,
                message.CreatedAt,
            });
        }
    }

    /// <summary>
    /// Đánh dấu message đã đọc realtime.
    /// </summary>
    public async Task MarkAsRead(Guid messageId)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            await Clients.Caller.SendAsync("error", "Bạn chưa đăng nhập.");
            return;
        }

        var result = await _chatService.MarkAsReadAsync(userId.Value, messageId);
        if (!result.IsSuccess)
        {
            await Clients.Caller.SendAsync("error", result.Message);
            return;
        }

        var message = await _chatService.GetMessageWithChatboxAsync(messageId, Context.ConnectionAborted);
        if (message is not null)
        {
            await Clients.Group($"chat-{message.ChatboxId}").SendAsync("messageMarkedAsRead", new { messageId });
        }
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
        await Clients.Group($"chat-{chatboxId}").SendAsync("userJoined", new { userId = userId.Value });
    }

    /// <summary>
    /// Rời khỏi chatbox group.
    /// </summary>
    public async Task LeaveChatbox(Guid chatboxId)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat-{chatboxId}");
        await Clients.Group($"chat-{chatboxId}").SendAsync("userLeft", new { userId = userId.Value });
    }
}
