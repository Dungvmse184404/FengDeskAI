using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;

namespace FengDeskAI.Application.Features.Chat.Services;

public interface IChatService
{
    /// <summary>Lấy hoặc tạo phòng 1-1 với user khác (không cần biết trước — chỉ truyền otherUserId).</summary>
    Task<IServiceResult<ChatboxResponse>> GetOrStartDirectAsync(Guid userId, string? userRole, Guid otherUserId, CancellationToken ct = default);

    /// <summary>Tạo phòng nhóm (creator = Owner). MemberUserIds tuỳ chọn.</summary>
    Task<IServiceResult<ChatboxResponse>> CreateGroupAsync(Guid userId, string? userRole, CreateGroupRequest request, CancellationToken ct = default);

    /// <summary>Thêm thành viên vào phòng (chỉ Owner).</summary>
    Task<IServiceResult> AddParticipantAsync(Guid userId, Guid chatboxId, AddParticipantRequest request, CancellationToken ct = default);

    /// <summary>Xoá thành viên khỏi phòng (chỉ Owner; không tự xoá Owner cuối).</summary>
    Task<IServiceResult> RemoveParticipantAsync(Guid userId, Guid chatboxId, Guid targetUserId, CancellationToken ct = default);

    /// <summary>Danh sách phòng của user hiện tại (paged).</summary>
    Task<IServiceResult<ChatboxListResponse>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default);

    /// <summary>Messages trong phòng (paged, mới nhất trước).</summary>
    Task<IServiceResult<(List<ChatMessageResponse> Items, int TotalCount, int Page, int PageSize)>> GetMessagesAsync(
        Guid userId, Guid chatboxId, PageRequest page, CancellationToken ct = default);

    /// <summary>Gửi message (text và/hoặc link ảnh). role/email suy ra SenderName.</summary>
    Task<IServiceResult<ChatMessageResponse>> SendMessageAsync(
        Guid userId, string? userRole, string? userEmail, Guid chatboxId, SendMessageRequest request, CancellationToken ct = default);

    /// <summary>Tải ảnh chat lên storage (Chat_images/{chatboxId}/...) và trả link.</summary>
    Task<IServiceResult<string>> UploadImageAsync(
        Guid userId, Guid chatboxId, Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>Đánh dấu cả phòng đã đọc (cập nhật LastReadAt của participant).</summary>
    Task<IServiceResult> MarkChatboxAsReadAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Bật/tắt bot AI tự trả lời trong phòng (chỉ Owner).</summary>
    Task<IServiceResult> SetAiEnabledAsync(Guid userId, Guid chatboxId, bool enabled, CancellationToken ct = default);

    /// <summary>Kiểm tra user có là thành viên phòng không.</summary>
    Task<IServiceResult> ValidateChatboxAccessAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    void RecordUserConnection(Guid userId, string connectionId);
    void RemoveUserConnection(Guid userId, string connectionId);
}
