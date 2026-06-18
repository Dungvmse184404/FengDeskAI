using System.Collections.Concurrent;
using AutoMapper;
using FengDeskAI.Application.Common.Constants;
using FengDeskAI.Application.Common.Media;
using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;
using FengDeskAI.Application.Interfaces.External;
using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Enums.Chat;
using ChatMessageEntity = FengDeskAI.Domain.Entities.Chat.ChatMessage;
using ChatMessageImageEntity = FengDeskAI.Domain.Entities.Chat.ChatMessageImage;

namespace FengDeskAI.Application.Features.Chat.Services;

public class ChatService : IChatService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IFileStorage _storage;
    private readonly IChatRealtimeNotifier _notifier;
    private readonly IAiBotQueue _botQueue;
    private static readonly ConcurrentDictionary<Guid, HashSet<string>> _userConnections = new();

    public ChatService(IUnitOfWork uow, IMapper mapper, IFileStorage storage, IChatRealtimeNotifier notifier, IAiBotQueue botQueue)
    {
        _uow = uow;
        _mapper = mapper;
        _storage = storage;
        _notifier = notifier;
        _botQueue = botQueue;
    }

    public async Task<IServiceResult<ChatboxResponse>> GetOrStartDirectAsync(
        Guid userId, string? userRole, Guid otherUserId, CancellationToken ct = default)
    {
        if (userId == otherUserId)
            return ServiceResult<ChatboxResponse>.Failure(ApiStatusCodes.BadRequest, "Không thể chat với chính mình.");

        var chatbox = await _uow.Chatboxes.GetOrCreateDirectAsync(userId, ChatSenderHelper.TypeFrom(userRole), otherUserId, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ChatboxResponse>.Success(_mapper.Map<ChatboxResponse>(chatbox));
    }

    public async Task<IServiceResult<ChatboxResponse>> CreateGroupAsync(Guid userId, string? userRole, CreateGroupRequest request, CancellationToken ct = default)
    {
        var members = (request.MemberUserIds ?? new List<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        var chatbox = await _uow.Chatboxes.CreateGroupAsync(userId, ChatSenderHelper.TypeFrom(userRole), request.Title, members, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ChatboxResponse>.Success(_mapper.Map<ChatboxResponse>(chatbox), "Đã tạo phòng nhóm.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> AddParticipantAsync(Guid userId, Guid chatboxId, AddParticipantRequest request, CancellationToken ct = default)
    {
        var owner = await _uow.Chatboxes.GetParticipantAsync(chatboxId, userId, ct);
        if (owner is null || owner.Role != ParticipantRole.Owner)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Chỉ chủ phòng mới được thêm thành viên.");
        if (request.UserId == Guid.Empty)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Thiếu UserId.");

        await _uow.Chatboxes.AddParticipantAsync(chatboxId, request.UserId, ParticipantType.Customer, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã thêm thành viên.");
    }

    public async Task<IServiceResult> RemoveParticipantAsync(Guid userId, Guid chatboxId, Guid targetUserId, CancellationToken ct = default)
    {
        var owner = await _uow.Chatboxes.GetParticipantAsync(chatboxId, userId, ct);
        if (owner is null || owner.Role != ParticipantRole.Owner)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Chỉ chủ phòng mới được xoá thành viên.");

        var target = await _uow.Chatboxes.GetParticipantAsync(chatboxId, targetUserId, ct);
        if (target is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Thành viên không có trong phòng.");
        if (target.Role == ParticipantRole.Owner)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Không thể xoá chủ phòng.");

        _uow.Chatboxes.RemoveParticipant(target);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xoá thành viên.");
    }

    public async Task<IServiceResult<ChatboxListResponse>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default)
    {
        var (chatboxes, total) = await _uow.Chatboxes.GetByUserAsync(userId, page.Page, page.PageSize, ct);
        return ServiceResult<ChatboxListResponse>.Success(new ChatboxListResponse
        {
            Items = _mapper.Map<List<ChatboxResponse>>(chatboxes),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)page.PageSize),
        });
    }

    public async Task<IServiceResult<PagedResult<ChatMessageResponse>>> GetMessagesAsync(
        Guid userId, Guid chatboxId, PageRequest page, CancellationToken ct = default)
    {
        if (!await _uow.Chatboxes.IsParticipantAsync(chatboxId, userId, ct))
            return ServiceResult<PagedResult<ChatMessageResponse>>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền xem phòng này.");

        var (messages, total) = await _uow.ChatMessages.GetByChatboxAsync(chatboxId, page.Page, page.PageSize, ct);
        var dtos = _mapper.Map<List<ChatMessageResponse>>(messages);
        return ServiceResult<PagedResult<ChatMessageResponse>>.Success(
            new PagedResult<ChatMessageResponse>(dtos, page.Page, page.PageSize, total));
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
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phòng chat.");
        if (!await _uow.Chatboxes.IsParticipantAsync(chatboxId, userId, ct))
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền gửi tin trong phòng này.");

        var message = new ChatMessageEntity
        {
            ChatboxId = chatboxId,
            SenderId = userId,
            SenderType = MessageSenderType.User,
            SenderName = ChatSenderHelper.NameFrom(userEmail),
            Content = content,
            Images = imageUrls.Select((url, i) => new ChatMessageImageEntity { Url = url, SortOrder = i }).ToList(),
        };
        await _uow.ChatMessages.AddAsync(message, ct);
        chatbox.UpdatedAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);

        var dto = _mapper.Map<ChatMessageResponse>(message);
        await _notifier.MessageReceivedAsync(new ChatMessageBroadcast(
            dto.Id, dto.ChatboxId, dto.SenderId, dto.SenderType.ToString(), dto.SenderName,
            dto.Content, dto.CreatedAt, dto.Images), ct);

        // Phòng bật bot AI → đẩy job nền để AI tự trả lời (không block người gửi).
        if (chatbox.IsAiEnabled)
            _botQueue.Enqueue(chatboxId);

        return ServiceResult<ChatMessageResponse>.Success(dto);
    }

    public async Task<IServiceResult> SetAiEnabledAsync(Guid userId, Guid chatboxId, bool enabled, CancellationToken ct = default)
    {
        var participant = await _uow.Chatboxes.GetParticipantAsync(chatboxId, userId, ct);
        if (participant is null || participant.Role != ParticipantRole.Owner)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Chỉ chủ phòng mới được bật/tắt bot AI.");

        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phòng chat.");

        chatbox.IsAiEnabled = enabled;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success(enabled ? "Đã bật bot AI cho phòng." : "Đã tắt bot AI cho phòng.");
    }

    public async Task<IServiceResult<string>> UploadImageAsync(
        Guid userId, Guid chatboxId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!await _uow.Chatboxes.IsParticipantAsync(chatboxId, userId, ct))
            return ServiceResult<string>.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền trong phòng này.");
        if (content is null || content.Length == 0)
            return ServiceResult<string>.Failure(ApiStatusCodes.BadRequest, "Vui lòng chọn tệp ảnh.");
        if (!ImageUpload.IsAllowed(contentType))
            return ServiceResult<string>.Failure(ApiStatusCodes.UnprocessableEntity, "Chỉ chấp nhận ảnh JPG, PNG, BMP hoặc GIF.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext)) ext = ImageUpload.ExtensionFor(contentType);
        var objectPath = $"Chat_images/{chatboxId}/{Guid.NewGuid():N}{ext}";

        var stored = await _storage.UploadAsync(objectPath, content, contentType, ct);
        return ServiceResult<string>.Success(stored.Url, "Tải ảnh thành công.");
    }

    public async Task<IServiceResult> MarkChatboxAsReadAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
    {
        var participant = await _uow.Chatboxes.GetParticipantAsync(chatboxId, userId, ct);
        if (participant is null)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền trong phòng này.");

        participant.LastReadAt = DateTime.UtcNow;
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã đánh dấu đã đọc.");
    }

    public async Task<IServiceResult> ValidateChatboxAccessAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
    {
        if (!await _uow.Chatboxes.IsParticipantAsync(chatboxId, userId, ct))
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền truy cập phòng này.");
        return ServiceResult.Success();
    }

    public void RecordUserConnection(Guid userId, string connectionId)
        => _userConnections.AddOrUpdate(userId, new HashSet<string> { connectionId },
            (_, set) => { set.Add(connectionId); return set; });

    public void RemoveUserConnection(Guid userId, string connectionId)
    {
        if (_userConnections.TryGetValue(userId, out var set))
        {
            set.Remove(connectionId);
            if (set.Count == 0) _userConnections.TryRemove(userId, out _);
        }
    }
}
