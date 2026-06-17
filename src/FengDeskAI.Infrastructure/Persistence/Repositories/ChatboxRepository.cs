using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Chat;
using FengDeskAI.Domain.Enums.Chat;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class ChatboxRepository : GenericRepository<Chatbox>, IChatboxRepository
{
    public ChatboxRepository(AppDbContext context) : base(context) { }

    public Task<Chatbox?> GetWithParticipantsAsync(Guid id, CancellationToken ct = default)
        => _set.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<(List<Chatbox> Items, int TotalCount)> GetByUserAsync(
        Guid userId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _set.Where(c => c.Participants.Any(p => p.UserId == userId));
        var total = await query.CountAsync(ct);
        var items = await query
            .Include(c => c.Participants)
            .Include(c => c.Messages).ThenInclude(m => m.Images)
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (items, total);
    }

    public async Task<Chatbox> GetOrCreateDirectAsync(
        Guid creatorId, ParticipantType creatorType, Guid otherUserId, CancellationToken ct = default)
    {
        var existing = await _set
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c =>
                !c.IsGroup &&
                c.Participants.Any(p => p.UserId == creatorId) &&
                c.Participants.Any(p => p.UserId == otherUserId) &&
                c.Participants.Count(p => p.UserId != null) == 2, ct);
        if (existing != null) return existing;

        var now = DateTime.UtcNow;
        var chatbox = new Chatbox
        {
            IsGroup = false,
            CreatedByUserId = creatorId,
            Participants =
            {
                new ChatboxParticipant { UserId = creatorId, ParticipantType = creatorType, Role = ParticipantRole.Owner, JoinedAt = now },
                new ChatboxParticipant { UserId = otherUserId, ParticipantType = ParticipantType.Customer, Role = ParticipantRole.Member, JoinedAt = now },
            },
        };
        await _set.AddAsync(chatbox, ct);
        return chatbox;
    }

    public async Task<Chatbox> GetOrCreateAssistantAsync(
        Guid userId, ParticipantType userType, Guid? productId, CancellationToken ct = default)
    {
        var existing = await _set
            .Include(c => c.Participants)
            .FirstOrDefaultAsync(c =>
                !c.IsGroup &&
                c.ProductId == productId &&
                c.Participants.Any(p => p.ParticipantType == ParticipantType.AiBot) &&
                c.Participants.Any(p => p.UserId == userId) &&
                c.Participants.Count(p => p.UserId != null) == 1, ct);
        if (existing != null) return existing;

        var now = DateTime.UtcNow;
        var chatbox = new Chatbox
        {
            IsGroup = false,
            ProductId = productId,
            CreatedByUserId = userId,
            Participants =
            {
                new ChatboxParticipant { UserId = userId, ParticipantType = userType, Role = ParticipantRole.Owner, JoinedAt = now },
                new ChatboxParticipant { UserId = null, ParticipantType = ParticipantType.AiBot, Role = ParticipantRole.Member, JoinedAt = now },
            },
        };
        await _set.AddAsync(chatbox, ct);
        return chatbox;
    }

    public Task<bool> IsParticipantAsync(Guid chatboxId, Guid userId, CancellationToken ct = default)
        => _context.Set<ChatboxParticipant>().AnyAsync(p => p.ChatboxId == chatboxId && p.UserId == userId, ct);

    public Task<ChatboxParticipant?> GetParticipantAsync(Guid chatboxId, Guid userId, CancellationToken ct = default)
        => _context.Set<ChatboxParticipant>().FirstOrDefaultAsync(p => p.ChatboxId == chatboxId && p.UserId == userId, ct);

    public async Task<Chatbox> CreateGroupAsync(
        Guid creatorId, ParticipantType creatorType, string? title, IEnumerable<Guid> memberUserIds, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var chatbox = new Chatbox
        {
            IsGroup = true,
            Title = title,
            CreatedByUserId = creatorId,
            Participants =
            {
                new ChatboxParticipant { UserId = creatorId, ParticipantType = creatorType, Role = ParticipantRole.Owner, JoinedAt = now },
            },
        };
        foreach (var uid in memberUserIds.Distinct().Where(u => u != creatorId))
            chatbox.Participants.Add(new ChatboxParticipant
            {
                UserId = uid, ParticipantType = ParticipantType.Customer, Role = ParticipantRole.Member, JoinedAt = now,
            });

        await _set.AddAsync(chatbox, ct);
        return chatbox;
    }

    public async Task AddParticipantAsync(Guid chatboxId, Guid userId, ParticipantType type, CancellationToken ct = default)
    {
        var set = _context.Set<ChatboxParticipant>();
        if (await set.AnyAsync(p => p.ChatboxId == chatboxId && p.UserId == userId, ct))
            return;
        await set.AddAsync(new ChatboxParticipant
        {
            ChatboxId = chatboxId, UserId = userId, ParticipantType = type,
            Role = ParticipantRole.Member, JoinedAt = DateTime.UtcNow,
        }, ct);
    }

    public void RemoveParticipant(ChatboxParticipant participant)
        => _context.Set<ChatboxParticipant>().Remove(participant);

    public Task<bool> HasOtherHumanAsync(Guid chatboxId, Guid userId, CancellationToken ct = default)
        => _context.Set<ChatboxParticipant>()
            .AnyAsync(p => p.ChatboxId == chatboxId && p.UserId != null && p.UserId != userId, ct);

    public async Task<List<Guid>> GetSharedRoomIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var set = _context.Set<ChatboxParticipant>();
        return await set
            .Where(p => p.UserId == userId)
            .Where(p => set.Any(o => o.ChatboxId == p.ChatboxId && o.UserId != null && o.UserId != userId))
            .Select(p => p.ChatboxId)
            .Distinct()
            .ToListAsync(ct);
    }
}
