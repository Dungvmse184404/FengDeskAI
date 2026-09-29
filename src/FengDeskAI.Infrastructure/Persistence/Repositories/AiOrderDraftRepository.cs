using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.CustomerCare;
using FengDeskAI.Domain.Enums.CustomerCare;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class AiOrderDraftRepository : IAiOrderDraftRepository
{
    private readonly AppDbContext _context;

    public AiOrderDraftRepository(AppDbContext context) => _context = context;

    private DbSet<AiOrderDraft> Drafts => _context.Set<AiOrderDraft>();

    private IQueryable<AiOrderDraft> Pending(Guid userId, Guid chatboxId, DateTime now)
        => Drafts.Where(d => d.UserId == userId && d.ChatboxId == chatboxId
                             && d.Status == AiOrderDraftStatus.Pending && d.ExpiresAt > now);

    public Task<AiOrderDraft?> GetPendingAsync(Guid userId, Guid chatboxId, DateTime now, CancellationToken ct = default)
        => Pending(userId, chatboxId, now).AsNoTracking().FirstOrDefaultAsync(ct);

    public Task<AiOrderDraft?> GetPendingForUpdateAsync(Guid userId, Guid chatboxId, DateTime now, CancellationToken ct = default)
        => Pending(userId, chatboxId, now).FirstOrDefaultAsync(ct);

    public async Task AddAsync(AiOrderDraft draft, CancellationToken ct = default)
        => await Drafts.AddAsync(draft, ct);

    // ExecuteUpdate/ExecuteDelete bỏ qua interceptor SaveChanges nên phải tự set UpdatedAt.
    public async Task<bool> TryClaimAsync(Guid draftId, DateTime now, CancellationToken ct = default)
        => await Drafts.Where(d => d.Id == draftId && d.Status == AiOrderDraftStatus.Pending)
            .ExecuteUpdateAsync(set => set
                .SetProperty(d => d.Status, AiOrderDraftStatus.Confirming)
                .SetProperty(d => d.UpdatedAt, now), ct) == 1;

    public async Task ReleaseClaimAsync(Guid draftId, DateTime now, CancellationToken ct = default)
        => await Drafts.Where(d => d.Id == draftId && d.Status == AiOrderDraftStatus.Confirming)
            .ExecuteUpdateAsync(set => set
                .SetProperty(d => d.Status, AiOrderDraftStatus.Pending)
                .SetProperty(d => d.UpdatedAt, now), ct);

    public async Task ReleaseClaimWithNewPriceAsync(
        Guid draftId, decimal unitPrice, decimal shippingFee, decimal totalAmount, DateTime now, CancellationToken ct = default)
        => await Drafts.Where(d => d.Id == draftId && d.Status == AiOrderDraftStatus.Confirming)
            .ExecuteUpdateAsync(set => set
                .SetProperty(d => d.Status, AiOrderDraftStatus.Pending)
                .SetProperty(d => d.UnitPriceSnapshot, unitPrice)
                .SetProperty(d => d.ShippingFeeSnapshot, shippingFee)
                .SetProperty(d => d.TotalAmountSnapshot, totalAmount)
                .SetProperty(d => d.UpdatedAt, now), ct);

    public Task<int> DeleteAsync(Guid draftId, CancellationToken ct = default)
        => Drafts.IgnoreQueryFilters().Where(d => d.Id == draftId).ExecuteDeleteAsync(ct);

    public Task<int> DeletePendingAsync(Guid userId, Guid chatboxId, CancellationToken ct = default)
        => Drafts.IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.ChatboxId == chatboxId && d.Status == AiOrderDraftStatus.Pending)
            .ExecuteDeleteAsync(ct);

    public Task<int> DeletePendingChangedSinceAsync(Guid chatboxId, DateTime since, CancellationToken ct = default)
        => Drafts.IgnoreQueryFilters()
            .Where(d => d.ChatboxId == chatboxId && d.Status == AiOrderDraftStatus.Pending && d.UpdatedAt >= since)
            .ExecuteDeleteAsync(ct);

    public Task<int> PurgeStaleAsync(DateTime now, TimeSpan confirmingGrace, CancellationToken ct = default)
    {
        var stuckBefore = now - confirmingGrace;
        return Drafts.IgnoreQueryFilters()
            .Where(d => (d.Status == AiOrderDraftStatus.Pending && d.ExpiresAt <= now)
                        || (d.Status == AiOrderDraftStatus.Confirming && d.UpdatedAt < stuckBefore)
                        || d.IsDeleted)
            .ExecuteDeleteAsync(ct);
    }
}
