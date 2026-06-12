using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Interfaces.Repositories;
using ChatboxEntity = FengDeskAI.Domain.Entities.Chat.Chatbox;
using ChatMessageEntity = FengDeskAI.Domain.Entities.Chat.ChatMessage;

namespace FengDeskAI.Application.Features.Chat.Services;

public class ChatService : IChatService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public ChatService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<IServiceResult<ChatboxResponse>> GetOrStartAsync(Guid userId, Guid otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId)
            return ServiceResult<ChatboxResponse>.Failure(ApiStatusCodes.BadRequest, "Không thể chat với chính mình.");

        var chatbox = await _uow.Chatboxes.GetOrCreateAsync(userId, otherUserId, ct);
        await _uow.SaveChangesAsync(ct);

        var dto = _mapper.Map<ChatboxResponse>(chatbox);
        return ServiceResult<ChatboxResponse>.Success(dto);
    }

    public async Task<IServiceResult<ChatboxListResponse>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var (chatboxes, total) = await _uow.Chatboxes.GetByUserAsync(userId, page.Page, page.PageSize, ct);
        var dtos = _mapper.Map<List<ChatboxResponse>>(chatboxes);

        return ServiceResult<ChatboxListResponse>.Success(new ChatboxListResponse
        {
            Items = dtos,
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)page.PageSize),
        });
    }

    public async Task<IServiceResult<(List<ChatMessageResponse> Items, int TotalCount, int Page, int PageSize)>> GetMessagesAsync(
        Guid userId, Guid chatboxId, PageRequest page, CancellationToken ct = default)
    {
        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult<(List<ChatMessageResponse>, int, int, int)>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy chatbox.");

        if (chatbox.SenderUserId != userId && chatbox.RecipientUserId != userId)
            return ServiceResult<(List<ChatMessageResponse>, int, int, int)>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xem chatbox này.");

        var (messages, total) = await _uow.ChatMessages.GetByChatboxAsync(chatboxId, page.Page, page.PageSize, ct);
        var dtos = _mapper.Map<List<ChatMessageResponse>>(messages);

        return ServiceResult<(List<ChatMessageResponse>, int, int, int)>.Success(
            (dtos, total, page.Page, page.PageSize));
    }

    public async Task<IServiceResult<ChatMessageResponse>> SendMessageAsync(
        Guid userId, Guid chatboxId, SendMessageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.BadRequest, "Nội dung tin nhắn không được trống.");

        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy chatbox.");

        if (chatbox.SenderUserId != userId && chatbox.RecipientUserId != userId)
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền gửi tin nhắn trong chatbox này.");

        var message = new ChatMessageEntity
        {
            ChatboxId = chatboxId,
            SenderUserId = userId,
            Content = request.Content.Trim(),
            IsRead = false,
        };

        await _uow.ChatMessages.AddAsync(message, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var dto = _mapper.Map<ChatMessageResponse>(message);
        return ServiceResult<ChatMessageResponse>.Success(dto);
    }

    public async Task<IServiceResult> MarkAsReadAsync(Guid userId, Guid messageId, CancellationToken ct = default)
    {
        var message = await _uow.ChatMessages.GetByIdAsync(messageId, ct);
        if (message is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tin nhắn.");

        var chatbox = await _uow.Chatboxes.GetByIdAsync(message.ChatboxId, ct);
        if (chatbox is null || (chatbox.SenderUserId != userId && chatbox.RecipientUserId != userId))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền đánh dấu tin nhắn này.");

        if (!message.IsRead)
        {
            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _uow.SaveChangesAsync(ct);
        }

        return ServiceResult.Success("Đã đánh dấu tin nhắn là đã đọc.");
    }

    public async Task<IServiceResult> MarkChatboxAsReadAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
    {
        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy chatbox.");

        if (chatbox.SenderUserId != userId && chatbox.RecipientUserId != userId)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền đánh dấu chatbox này.");

        var unread = await _uow.ChatMessages.GetUnreadInChatboxAsync(chatboxId, userId, ct);
        if (unread.Count == 0)
            return ServiceResult.Success("Không có tin nhắn chưa đọc.");

        var now = DateTime.UtcNow;
        foreach (var msg in unread)
        {
            msg.IsRead = true;
            msg.ReadAt = now;
        }

        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success($"Đã đánh dấu {unread.Count} tin nhắn là đã đọc.");
    }
}
