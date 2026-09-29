using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class PlatformFeeRateRepository : GenericRepository<PlatformFeeRate>, IPlatformFeeRateRepository
{
    public PlatformFeeRateRepository(AppDbContext context) : base(context) { }

    public Task<PlatformFeeRate?> GetEffectiveAsync(DateTime nowUtc, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Where(r => r.EffectiveFrom <= nowUtc)
            .OrderByDescending(r => r.EffectiveFrom).ThenByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<List<PlatformFeeRateHistoryRow>> GetHistoryAsync(int take, CancellationToken ct = default)
        => (from r in _set.AsNoTracking()
            join u in _context.Users.AsNoTracking() on r.CreatedBy equals u.Id into changedBy
            from u in changedBy.DefaultIfEmpty()
            orderby r.EffectiveFrom descending, r.CreatedAt descending
            select new PlatformFeeRateHistoryRow(r.Id, r.CommissionRate, r.EffectiveFrom, r.Note, r.CreatedAt,
                u == null ? null : u.FullName))
            .Take(take)
            .ToListAsync(ct);
}
