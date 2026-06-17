using FengDeskAI.Domain.Entities.Chat;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IChatboxRepository : IGenericRepository<Chatbox>
{
    /// <summary>Lấy chatbox Direct giữa 2 user (bất kể thứ tự sender/recipient).</summary>
    Task<Chatbox?> GetBetweenUsersAsync(Guid userId1, Guid userId2, CancellationToken ct = default);

    /// <summary>Lấy danh sách chatbox của user (kèm last message), paged.</summary>
    Task<(List<Chatbox> Items, int TotalCount)> GetByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Hoặc tạo mới chatbox Direct nếu chưa có.</summary>
    Task<Chatbox> GetOrCreateAsync(Guid userId1, Guid userId2, CancellationToken ct = default);

    /// <summary>Lấy hoặc tạo hội thoại với AI cho user (mỗi sản phẩm một hội thoại; productId null = trò chuyện chung).</summary>
    Task<Chatbox> GetOrCreateAssistantAsync(Guid userId, Guid? productId, CancellationToken ct = default);
}
