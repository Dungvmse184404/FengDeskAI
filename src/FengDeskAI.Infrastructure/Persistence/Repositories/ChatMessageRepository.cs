using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ChatMessageRepository : GenericRepository<ChatMessage>, IChatMessageRepository
{
    public ChatMessageRepository(AppDbContext context) : base(context) { }

    public async Task<(List<ChatMessage> Items, int TotalCount)> GetByChatboxAsync(
        Guid chatboxId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().Where(m => m.ChatboxId == chatboxId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(m => m.Images)
            // ThenBy Id: CreatedAt không duy nhất (nhiều tin cùng mốc) → thêm khóa phụ để phân trang ỔN ĐỊNH,
            // tránh cùng 1 tin lọt 2 trang (gốc gây "two children with the same key" ở FE).
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<List<ChatMessage>> GetRecentAsync(Guid chatboxId, int count, CancellationToken ct = default)
    {
        var items = await _set.Where(m => m.ChatboxId == chatboxId)
            .Include(m => m.Images)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(count)
            .ToListAsync(ct);
        items.Reverse();
        return items;
    }

    public Task<ChatMessage?> GetByIdWithImagesAsync(Guid id, CancellationToken ct = default)
        => _set.Include(m => m.Images).FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task SoftDeleteFromAsync(Guid chatboxId, DateTime fromCreatedAt, Guid fromId, CancellationToken ct = default)
        => _set
            .Where(m => m.ChatboxId == chatboxId
                && (m.CreatedAt > fromCreatedAt || (m.CreatedAt == fromCreatedAt && m.Id >= fromId)))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsDeleted, true), ct);
}
