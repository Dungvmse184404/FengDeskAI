using FengDeskAI.Domain.Entities.Chat;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface IChatboxRepository : IGenericRepository<Chatbox>
{
    /// <summary>Lấy chatbox giữa 2 user (bất kể thứ tự sender/recipient).</summary>
    Task<Chatbox?> GetBetweenUsersAsync(Guid userId1, Guid userId2, CancellationToken ct = default);

    /// <summary>Lấy danh sách chatbox của user (kèm last message), paged.</summary>
    Task<(List<Chatbox> Items, int TotalCount)> GetByUserAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Hoặc tạo mới nếu chưa có.</summary>
    Task<Chatbox> GetOrCreateAsync(Guid userId1, Guid userId2, CancellationToken ct = default);
}
