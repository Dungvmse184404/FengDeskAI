using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Enums.Chat;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ChatboxRepository : GenericRepository<Chatbox>, IChatboxRepository
{
    public ChatboxRepository(AppDbContext context) : base(context) { }

    public Task<Chatbox?> GetBetweenUsersAsync(Guid userId1, Guid userId2, CancellationToken ct = default)
        => _set.Include(c => c.Messages)
               .FirstOrDefaultAsync(c =>
                   c.Type == ChatboxType.Direct &&
                   ((c.SenderUserId == userId1 && c.RecipientUserId == userId2) ||
                    (c.SenderUserId == userId2 && c.RecipientUserId == userId1)), ct);

    public async Task<(List<Chatbox> Items, int TotalCount)> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _set.Where(c => c.SenderUserId == userId || c.RecipientUserId == userId);
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Messages).ThenInclude(m => m.Images)
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

        // Lưu cặp người dùng theo thứ tự chuẩn hoá (id nhỏ hơn luôn là SenderUserId) để chỉ mục
        // unique (SenderUserId, RecipientUserId) đại diện cho cặp KHÔNG có thứ tự. Nhờ vậy hai request
        // đồng thời tạo A→B và B→A sẽ sinh ra cùng một bộ khoá và bị unique index chặn bản ghi trùng.
        var (first, second) = userId1.CompareTo(userId2) <= 0 ? (userId1, userId2) : (userId2, userId1);

        var chatbox = new Chatbox
        {
            Type = ChatboxType.Direct,
            SenderUserId = first,
            RecipientUserId = second,
        };
        await _set.AddAsync(chatbox, ct);
        return chatbox;
    }

    public async Task<Chatbox> GetOrCreateAssistantAsync(Guid userId, Guid? productId, CancellationToken ct = default)
    {
        var existing = await _set.FirstOrDefaultAsync(c =>
            c.Type == ChatboxType.Assistant &&
            c.SenderUserId == userId &&
            c.ProductId == productId, ct);
        if (existing != null)
            return existing;

        var chatbox = new Chatbox
        {
            Type = ChatboxType.Assistant,
            SenderUserId = userId,
            RecipientUserId = null,
            ProductId = productId,
        };
        await _set.AddAsync(chatbox, ct);
        return chatbox;
    }
}
