using FengDeskAI.Domain.Entities.Announcement;

namespace FengDeskAI.Application.Interfaces.Repositories;

public interface INotificationRepository : IGenericRepository<Notification>
{
    /// <summary>Lấy danh sách thông báo của user, sắp xếp mới nhất trước, hỗ trợ lọc chưa đọc.</summary>
    Task<(List<Notification> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken ct = default);

    Task<Notification?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default);

    /// <summary>Đếm số thông báo chưa đọc — dùng cho badge.</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Lấy toàn bộ thông báo chưa đọc của user để mark-all-read.</summary>
    Task<List<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken ct = default);
}
