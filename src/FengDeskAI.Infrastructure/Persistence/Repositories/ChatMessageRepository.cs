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
        items.Reverse(); // trả về cũ → mới để dựng hội thoại theo trình tự
        return items;
    }

    // Tin "chưa đọc với mình" = không phải do mình gửi → gồm cả tin của AI (sender_user_id NULL).
    public Task<List<ChatMessage>> GetUnreadInChatboxAsync(Guid chatboxId, Guid userId, CancellationToken ct = default)
        => _set.Where(m => m.ChatboxId == chatboxId && !m.IsRead
                           && (m.SenderUserId == null || m.SenderUserId != userId))
               .ToListAsync(ct);

    public Task<int> CountUnreadInChatboxAsync(Guid chatboxId, Guid userId, CancellationToken ct = default)
        => _set.CountAsync(m => m.ChatboxId == chatboxId && !m.IsRead
                                && (m.SenderUserId == null || m.SenderUserId != userId), ct);
}
