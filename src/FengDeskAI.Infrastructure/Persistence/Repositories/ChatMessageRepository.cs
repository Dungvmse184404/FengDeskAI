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
        var query = _set.Where(m => m.ChatboxId == chatboxId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(m => m.Images)
            .OrderByDescending(m => m.CreatedAt)
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
            .Take(count)
            .ToListAsync(ct);
        items.Reverse();
        return items;
    }
}
