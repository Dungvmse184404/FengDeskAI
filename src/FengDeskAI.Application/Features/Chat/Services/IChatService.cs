using FengDeskAI.Application.Common.Models;
using FengDeskAI.Application.Common.Results;
using FengDeskAI.Application.Features.Chat.DTOs;

namespace FengDeskAI.Application.Features.Chat.Services;

public interface IChatService
{
    /// <summary>Lấy hoặc tạo chatbox với user khác.</summary>
    Task<IServiceResult<ChatboxResponse>> GetOrStartAsync(Guid userId, Guid otherUserId, CancellationToken ct = default);

    /// <summary>Danh sách chatbox của user hiện tại (paged).</summary>
    Task<IServiceResult<ChatboxListResponse>> GetMineAsync(Guid userId, PageRequest page, CancellationToken ct = default);

    /// <summary>Danh sách messages trong chatbox (paged, mới nhất trước).</summary>
    Task<IServiceResult<(List<ChatMessageResponse> Items, int TotalCount, int Page, int PageSize)>> GetMessagesAsync(
        Guid userId, Guid chatboxId, PageRequest page, CancellationToken ct = default);

    /// <summary>Gửi message (tự động được tạo với SenderUserId = userId).</summary>
    Task<IServiceResult<ChatMessageResponse>> SendMessageAsync(
        Guid userId, Guid chatboxId, SendMessageRequest request, CancellationToken ct = default);

    /// <summary>Đánh dấu 1 message đã đọc.</summary>
    Task<IServiceResult> MarkAsReadAsync(Guid userId, Guid messageId, CancellationToken ct = default);

    /// <summary>Đánh dấu tất cả message chưa đọc trong chatbox là đã đọc.</summary>
    Task<IServiceResult> MarkChatboxAsReadAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Kiểm tra user có quyền truy cập chatbox không.</summary>
    Task<IServiceResult> ValidateChatboxAccessAsync(Guid userId, Guid chatboxId, CancellationToken ct = default);

    /// <summary>Lấy message với thông tin chatbox (dùng cho realtime broadcast).</summary>
    Task<ChatMessageWithChatboxResponse?> GetMessageWithChatboxAsync(Guid messageId, CancellationToken ct = default);

    /// <summary>Ghi nhận connection của user (tracking online status).</summary>
    void RecordUserConnection(Guid userId, string connectionId);

    /// <summary>Xoá connection của user.</summary>
    void RemoveUserConnection(Guid userId, string connectionId);
}
