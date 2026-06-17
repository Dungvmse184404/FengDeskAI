using FengDeskAI.Domain.Entities.Chat;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IChatMessageRepository : IGenericRepository<ChatMessage>
{
    /// <summary>Messages của phòng, paged, mới nhất trước (kèm ảnh).</summary>
    Task<(List<ChatMessage> Items, int TotalCount)> GetByChatboxAsync(
        Guid chatboxId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>N tin gần nhất theo thứ tự cũ → mới (kèm ảnh) — feed AI / multi-room context.</summary>
    Task<List<ChatMessage>> GetRecentAsync(Guid chatboxId, int count, CancellationToken ct = default);
}
