using FengDeskAI.Domain.Entities.Chat;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IChatMessageRepository : IGenericRepository<ChatMessage>
{
    /// <summary>Messages của phòng, paged, mới nhất trước (kèm ảnh).</summary>
    Task<(List<ChatMessage> Items, int TotalCount)> GetByChatboxAsync(
        Guid chatboxId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>N tin gần nhất theo thứ tự cũ → mới (kèm ảnh) — feed AI / multi-room context.</summary>
    Task<List<ChatMessage>> GetRecentAsync(Guid chatboxId, int count, CancellationToken ct = default);

    /// <summary>1 tin theo Id kèm ảnh — dùng cho rewind (đọc nội dung/ảnh cũ trước khi cắt).</summary>
    Task<ChatMessage?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Soft-delete mọi tin của <paramref name="chatboxId"/> từ mốc (<paramref name="fromCreatedAt"/>,
    /// <paramref name="fromId"/>) trở đi (so sánh tuple, Id tie-break tin cùng timestamp) — dùng cho rewind.
    /// </summary>
    Task SoftDeleteFromAsync(Guid chatboxId, DateTime fromCreatedAt, Guid fromId, CancellationToken ct = default);
}
