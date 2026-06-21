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

    public async Task<IServiceResult<ChatboxResponse>> EnsureAssistantAsync(
        Guid userId, string? userRole, Guid? productId, CancellationToken ct = default)
    {
        var chatbox = await _uow.Chatboxes.GetOrCreateAssistantAsync(
            userId, ChatSenderHelper.TypeFrom(userRole), productId, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ChatboxResponse>.Success(_mapper.Map<ChatboxResponse>(chatbox));
    }

    public async Task<IServiceResult<ChatboxResponse>> GetOrStartSupportAsync(Guid userId, string? userRole, bool forceNew, CancellationToken ct = default)
    {
        var type = ChatSenderHelper.TypeFrom(userRole);
        var chatbox = forceNew
            ? await _uow.Chatboxes.CreateSupportRoomAsync(userId, type, ct)
            : await _uow.Chatboxes.GetOrCreateSupportRoomAsync(userId, type, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ChatboxResponse>.Success(_mapper.Map<ChatboxResponse>(chatbox));
    }

    public async Task<IServiceResult> DeleteChatboxAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
    {
        var participant = await _uow.Chatboxes.GetParticipantAsync(chatboxId, userId, ct);
        if (participant is null || participant.Role != ParticipantRole.Owner)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Chỉ chủ phòng mới được xóa cuộc trò chuyện.");

        var chatbox = await _uow.Chatboxes.GetByIdAsync(chatboxId, ct);
        if (chatbox is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phòng.");

        // Soft-delete (IsDeleted=true) cho mọi phòng. Phân biệt ở lúc đọc: phòng còn tin nhắn → hiện mờ (đóng);
        // phòng rỗng → biến mất khỏi danh sách + hàng đợi (coi như xóa hẳn).
        _uow.Chatboxes.Remove(chatbox);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult.Success("Đã xóa cuộc trò chuyện.");
    }

    public async Task<IServiceResult<ChatboxListResponse>> GetOpenSupportRoomsAsync(PageRequest page, CancellationToken ct = default)
    {
        var (rooms, total) = await _uow.Chatboxes.GetOpenSupportRoomsAsync(page.Page, page.PageSize, ct);
        return ServiceResult<ChatboxListResponse>.Success(new ChatboxListResponse
        {
            Items = _mapper.Map<List<ChatboxResponse>>(rooms),
            Page = page.Page,
            PageSize = page.PageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)page.PageSize),
        });
    }

    public async Task<IServiceResult<ChatboxResponse>> CreateGroupAsync(Guid userId, string? userRole, CreateGroupRequest request, CancellationToken ct = default)
    {
        var members = (request.MemberUserIds ?? new List<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        var chatbox = await _uow.Chatboxes.CreateGroupAsync(userId, ChatSenderHelper.TypeFrom(userRole), request.Title, members, ct);
        await _uow.SaveChangesAsync(ct);
        return ServiceResult<ChatboxResponse>.Success(_mapper.Map<ChatboxResponse>(chatbox), "Đã tạo phòng nhóm.", ApiStatusCodes.Created);
    }

    public async Task<IServiceResult> AddParticipantAsync(Guid callerId, ParticipantType callerType, Guid chatboxId, AddParticipantRequest request, CancellationToken ct = default)
    {
        if (request.UserId == Guid.Empty)
            return ServiceResult.Failure(ApiStatusCodes.BadRequest, "Thiếu UserId.");

        var room = await _uow.Chatboxes.GetWithParticipantsAsync(chatboxId, ct);
        if (room is null)
            return ServiceResult.Failure(ApiStatusCodes.NotFound, "Không tìm thấy phòng chat.");

        var caller = room.Participants.FirstOrDefault(p => p.UserId == callerId);
        var callerIsStaff = callerType is ParticipantType.Staff or ParticipantType.Manager or ParticipantType.Admin;
        var isOwner = caller?.Role == ParticipantRole.Owner;
        var isStaffMember = caller is not null &&
            caller.ParticipantType is ParticipantType.Staff or ParticipantType.Manager or ParticipantType.Admin;

        // Owner luôn được; staff đã ở trong phòng được; staff (theo JWT) được tham gia/mời vào phòng hỗ trợ.
        var canManage = isOwner || isStaffMember || (callerIsStaff && room.IsSupport);
        if (!canManage)
            return ServiceResult.Failure(ApiStatusCodes.Forbidden, "Bạn không có quyền thêm thành viên vào phòng này.");

        // Staff tự tham gia → gắn đúng type của họ; mời người khác → mặc định Customer.
        var newType = request.UserId == callerId && callerIsStaff ? callerType : ParticipantType.Customer;
        await _uow.Chatboxes.AddParticipantAsync(chatboxId, request.UserId, newType, ct);
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
        var items = _mapper.Map<List<ChatboxResponse>>(chatboxes);

        // Tính UnreadCount cho NGƯỜI GỌI: tin của người khác có CreatedAt > LastReadAt của họ.
        // AutoMapper giữ thứ tự → Zip khớp entity ↔ dto.
        foreach (var (entity, dto) in chatboxes.Zip(items))
        {
            var me = entity.Participants.FirstOrDefault(p => p.UserId == userId);
            var lastRead = me?.LastReadAt ?? DateTime.MinValue;
            dto.UnreadCount = entity.Messages.Count(
                m => !m.IsDeleted && m.SenderId != userId && m.CreatedAt > lastRead);
        }

        return ServiceResult<ChatboxListResponse>.Success(new ChatboxListResponse
        {
            Items = items,
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
        if (chatbox.IsDeleted)
            return ServiceResult<ChatMessageResponse>.Failure(ApiStatusCodes.Conflict, "Cuộc trò chuyện đã đóng, không thể gửi tin mới.");
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

        // Có người gọi @AI → đẩy job nền để AI trả lời (scope tool/ngữ cảnh theo người gọi; không block người gửi).
        if (AiMention.Mentions(content))
            _botQueue.Enqueue(new AiBotJob(chatboxId, userId));

        return ServiceResult<ChatMessageResponse>.Success(dto);
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

    public async Task<IServiceResult<ChatConsentResponse>> GetMyConsentAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
    {
        if (!await _uow.Chatboxes.IsParticipantAsync(chatboxId, userId, ct))
            return ServiceResult<ChatConsentResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có trong phòng này.");

        var consent = await _uow.Chatboxes.GetConsentAsync(chatboxId, userId, ct);
        // Mặc định CHIA SẺ (opt-out): chưa có bản ghi → coi như cho phép tất cả.
        return ServiceResult<ChatConsentResponse>.Success(new ChatConsentResponse
        {
            ShareProfile = consent?.ShareProfile ?? true,
            ShareWorkspaces = consent?.ShareWorkspaces ?? true,
            ShareOrders = consent?.ShareOrders ?? true,
        });
    }

    public async Task<IServiceResult<ChatConsentResponse>> SetMyConsentAsync(Guid userId, Guid chatboxId, SetChatConsentRequest request, CancellationToken ct = default)
    {
        if (!await _uow.Chatboxes.IsParticipantAsync(chatboxId, userId, ct))
            return ServiceResult<ChatConsentResponse>.Failure(ApiStatusCodes.Forbidden, "Bạn không có trong phòng này.");

        var consent = await _uow.Chatboxes.GetConsentAsync(chatboxId, userId, ct);
        if (consent is null)
        {
            consent = new Domain.Entities.Chat.ChatRoomDataConsent { ChatboxId = chatboxId, GranterUserId = userId };
            await _uow.Chatboxes.AddConsentAsync(consent, ct);
        }
        consent.ShareProfile = request.ShareProfile;
        consent.ShareWorkspaces = request.ShareWorkspaces;
        consent.ShareOrders = request.ShareOrders;
        await _uow.SaveChangesAsync(ct);

        return ServiceResult<ChatConsentResponse>.Success(new ChatConsentResponse
        {
            ShareProfile = consent.ShareProfile,
            ShareWorkspaces = consent.ShareWorkspaces,
            ShareOrders = consent.ShareOrders,
        }, "Đã cập nhật quyền chia sẻ.");
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
