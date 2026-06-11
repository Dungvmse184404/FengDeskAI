using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Notification;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class NotificationRepository : GenericRepository<Notification>, INotificationRepository
{
    public NotificationRepository(AppDbContext context) : base(context) { }

    public async Task<(List<Notification> Items, int TotalCount)> GetPagedByUserAsync(
        Guid userId, int page, int pageSize, bool? unreadOnly = null, CancellationToken ct = default)
    {
        var query = _set.Where(n => n.UserId == userId);

        if (unreadOnly == true)
            query = query.Where(n => !n.IsRead);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public Task<Notification?> GetByIdAndUserAsync(Guid id, Guid userId, CancellationToken ct = default)
        => _set.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, ct);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
        => _set.CountAsync(n => n.UserId == userId && !n.IsRead, ct);

    public Task<List<Notification>> GetUnreadByUserAsync(Guid userId, CancellationToken ct = default)
        => _set.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
}
