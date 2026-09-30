using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Promotion;
using FengDeskAI.Domain.Enums.Promotion;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class VoucherRepository : GenericRepository<Voucher>, IVoucherRepository
{
    public VoucherRepository(AppDbContext context) : base(context) { }

    private DbSet<VoucherRedemption> Redemptions => _context.Set<VoucherRedemption>();

    public Task<Voucher?> GetByCodeAsync(string normalizedCode, CancellationToken ct = default)
        => _set.AsNoTracking().FirstOrDefaultAsync(v => v.Code == normalizedCode, ct);

    public Task<List<Voucher>> GetActiveAutoApplyAsync(CancellationToken ct = default)
        => _set.AsNoTracking().Where(v => v.IsActive && v.IsAutoApply).ToListAsync(ct);

    public Task<List<Voucher>> GetAvailableAsync(DateTime nowUtc, CancellationToken ct = default)
        => _set.AsNoTracking()
            .Where(v => v.IsActive
                        && (v.StartsAt == null || v.StartsAt <= nowUtc)
                        && (v.EndsAt == null || v.EndsAt >= nowUtc)
                        && (v.UsageLimit == null || v.UsedCount < v.UsageLimit))
            .OrderBy(v => v.MinOrderSubtotal)
            .ToListAsync(ct);

    public async Task<(List<Voucher> Items, int Total)> GetPagedAsync(int skip, int take, CancellationToken ct = default)
    {
        var query = _set.AsNoTracking().OrderByDescending(v => v.CreatedAt);
        return (await query.Skip(skip).Take(take).ToListAsync(ct), await query.CountAsync(ct));
    }

    public Task<int> CountActiveRedemptionsAsync(Guid voucherId, Guid customerId, CancellationToken ct = default)
        => Redemptions.CountAsync(r => r.VoucherId == voucherId && r.CustomerId == customerId
                                      && r.Status == VoucherRedemptionStatus.Applied, ct);

    public async Task<bool> TryIncrementUsageAsync(Guid voucherId, CancellationToken ct = default)
        => await _set.Where(v => v.Id == voucherId && v.IsActive
                                 && (v.UsageLimit == null || v.UsedCount < v.UsageLimit))
               .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1), ct) == 1;

    public Task DecrementUsageAsync(Guid voucherId, CancellationToken ct = default)
        => _set.Where(v => v.Id == voucherId && v.UsedCount > 0)
               .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount - 1), ct);

    public Task ForceIncrementUsageAsync(Guid voucherId, CancellationToken ct = default)
        => _set.Where(v => v.Id == voucherId)
               .ExecuteUpdateAsync(s => s.SetProperty(v => v.UsedCount, v => v.UsedCount + 1), ct);

    public async Task AddRedemptionAsync(VoucherRedemption redemption, CancellationToken ct = default)
        => await Redemptions.AddAsync(redemption, ct);

    public Task<VoucherRedemption?> GetRedemptionByOrderAsync(Guid orderId, CancellationToken ct = default)
        => Redemptions.FirstOrDefaultAsync(r => r.OrderId == orderId, ct);
}
