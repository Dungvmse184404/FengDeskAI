using FengDeskAI.Domain.Entities.Chat;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IChatMessageRepository : IGenericRepository<ChatMessage>
{
    /// <summary>Lấy messages của chatbox, paged, mới nhất trước (kèm ảnh).</summary>
    Task<(List<ChatMessage> Items, int TotalCount)> GetByChatboxAsync(
        Guid chatboxId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Lấy N tin nhắn gần nhất của chatbox theo thứ tự CŨ → MỚI (kèm ảnh) — để feed lịch sử cho AI.</summary>
    Task<List<ChatMessage>> GetRecentAsync(Guid chatboxId, int count, CancellationToken ct = default);

    /// <summary>Lấy unread messages của user trong chatbox (những tin người khác gửi).</summary>
    Task<List<ChatMessage>> GetUnreadInChatboxAsync(Guid chatboxId, Guid userId, CancellationToken ct = default);

    /// <summary>Đếm unread messages trong chatbox.</summary>
    Task<int> CountUnreadInChatboxAsync(Guid chatboxId, Guid userId, CancellationToken ct = default);
}
