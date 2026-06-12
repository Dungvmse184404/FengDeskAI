using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ChatboxRepository : GenericRepository<Chatbox>, IChatboxRepository
{
    public ChatboxRepository(AppDbContext context) : base(context) { }

    public Task<Chatbox?> GetBetweenUsersAsync(Guid userId1, Guid userId2, CancellationToken ct = default)
        => _set.Include(c => c.Messages)
               .FirstOrDefaultAsync(c =>
                   (c.SenderUserId == userId1 && c.RecipientUserId == userId2) ||
                   (c.SenderUserId == userId2 && c.RecipientUserId == userId1), ct);

    public async Task<(List<Chatbox> Items, int TotalCount)> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _set.Where(c => c.SenderUserId == userId || c.RecipientUserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Messages)
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<Chatbox> GetOrCreateAsync(Guid userId1, Guid userId2, CancellationToken ct = default)
    {
        var existing = await GetBetweenUsersAsync(userId1, userId2, ct);
        if (existing != null)
            return existing;

        var chatbox = new Chatbox
        {
            SenderUserId = userId1,
            RecipientUserId = userId2,
        };
        await _set.AddAsync(chatbox, ct);
        return chatbox;
    }
}
