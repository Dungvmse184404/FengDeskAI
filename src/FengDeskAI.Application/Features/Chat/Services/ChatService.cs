using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Media;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using System.Collections.Concurrent;
using ChatboxEntity = FengDeskAI.Domain.Entities.Chat.Chatbox;
using ChatMessageEntity = FengDeskAI.Domain.Entities.Chat.ChatMessage;
using ChatMessageImageEntity = FengDeskAI.Domain.Entities.Chat.ChatMessageImage;

namespace FengDeskAI.Application.Features.Chat.Services;

public class ChatService : IChatService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IFileStorage _storage;
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();

    public ChatService(IUnitOfWork uow, IMapper mapper, IFileStorage storage)
    {
        _uow = uow;
        _mapper = mapper;
        _storage = storage;
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

        if (!IsParticipant(chatbox, userId))
            return ServiceResult<(List<ChatMessageResponse>, int, int, int)>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xem chatbox này.");

        var (messages, total) = await _uow.ChatMessages.GetByChatboxAsync(chatboxId, page.Page, page.PageSize, ct);
        var dtos = _mapper.Map<List<ChatMessageResponse>>(messages);

        return ServiceResult<(List<ChatMessageResponse>, int, int, int)>.Success(
            (dtos, total, page.Page, page.PageSize));
    }

    public async Task<IServiceResult<ChatMessageResponse>> SendMessageAsync(
        Guid userId, string? userRole, string? userEmail, Guid chatboxId, SendMessageRequest request, CancellationToken ct = default)
    {
        var content = string.IsNullOrWhiteSpace(request.Content) ? null : request.Content.Trim();
        var imageUrls = request.ImageUrls?.Where(u => !string.IsNullOrWhiteSpace(u)).ToList() ?? new List<string>();

        if (content is null && imageUrls.Count == 0)
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.BadRequest, "Tin nhắn phải có nội dung hoặc ảnh.");

        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy chatbox.");

        if (!IsParticipant(chatbox, userId))
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền gửi tin nhắn trong chatbox này.");

        var message = new ChatMessageEntity
        {
            ChatboxId = chatboxId,
            SenderUserId = userId,
            SenderRole = ChatSenderHelper.RoleFrom(userRole),
            SenderName = ChatSenderHelper.NameFrom(userEmail),
            Content = content,
            IsFromAi = false,
            IsRead = false,
            Images = imageUrls.Select((url, i) => new ChatMessageImageEntity { Url = url, SortOrder = i }).ToList(),
        };

        await _uow.ChatMessages.AddAsync(message, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var dto = _mapper.Map<ChatMessageResponse>(message);
        return ServiceResult<ChatMessageResponse>.Success(dto);
    }

    public async Task<IServiceResult<string>> UploadImageAsync(
        Guid userId, Guid chatboxId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult<string>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy chatbox.");
        if (!IsParticipant(chatbox, userId))
            return ServiceResult<string>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền gửi ảnh trong chatbox này.");

        if (content is null || content.Length == 0)
            return ServiceResult<string>.Failure(ApiStatusCodes.BadRequest, "Vui lòng chọn tệp ảnh.");
        if (!ImageUpload.IsAllowed(contentType))
            return ServiceResult<string>.Failure(ApiStatusCodes.UnprocessableEntity, "Chỉ chấp nhận ảnh JPEG, PNG, WEBP hoặc GIF.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(contentType);
        var objectPath = $"Chat_images/{chatboxId}/{Guid.NewGuid():N}{ext}";

        var stored = await _storage.UploadAsync(objectPath, content, contentType, ct);
        return ServiceResult<string>.Success(stored.Url, "Tải ảnh thành công.");
    }

    public async Task<IServiceResult> MarkAsReadAsync(Guid userId, Guid messageId, CancellationToken ct = default)
    {
        var message = await _uow.ChatMessages.GetByIdAsync(messageId, ct);
        if (message is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy tin nhắn.");

        var chatbox = await _uow.Chatboxes.GetByIdAsync(message.ChatboxId, ct);
        if (chatbox is null || !IsParticipant(chatbox, userId))
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

        if (!IsParticipant(chatbox, userId))
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

    public async Task<IServiceResult> ValidateChatboxAccessAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
    {
        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy chatbox.");

        if (!IsParticipant(chatbox, userId))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền truy cập chatbox này.");

        return ServiceResult.Success();
    }

    public async Task<ChatMessageWithChatboxResponse?> GetMessageWithChatboxAsync(Guid messageId, CancellationToken ct = default)
    {
        var message = await _uow.ChatMessages.GetByIdAsync(messageId, ct);
        if (message is null)
            return null;

        return new ChatMessageWithChatboxResponse
        {
            Id = message.Id,
            ChatboxId = message.ChatboxId,
            SenderUserId = message.SenderUserId,
            SenderRole = message.SenderRole,
            SenderName = message.SenderName,
            Content = message.Content,
            IsFromAi = message.IsFromAi,
            IsRead = message.IsRead,
            ReadAt = message.ReadAt,
            CreatedAt = message.CreatedAt,
            Images = message.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
        };
    }

    /// <summary>User là một bên của hội thoại (sender hoặc recipient). Hội thoại AI: chỉ sender.</summary>
    private static bool IsParticipant(ChatboxEntity chatbox, Guid userId)
        => chatbox.SenderUserId == userId || chatbox.RecipientUserId == userId;

    public void RecordUserConnection(Guid userId, string connectionId)
    {
        _userConnections.AddOrUpdate(
            userId,
            new HashSet<string> { connectionId },
            (_, connections) =>
            {
                connections.Add(connectionId);
                return connections;
            });
    }

    public void RemoveUserConnection(Guid userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            connections.Remove(connectionId);
            if (connections.Count == 0)
                _userConnections.TryRemove(userId, out _);
        }
    }
}
