using FengDeskAI.Application.Interfaces.Repositories;
using FengDeskAI.Domain.Entities.Payment;
using FengDeskAI.Domain.Enums.Payment;
using FengDeskAI.Infrastructure.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FengDeskAI.Infrastructure.Persistence.Repositories;

public class LedgerRepository : GenericRepository<LedgerEntry>, ILedgerRepository
{
    public LedgerRepository(AppDbContext context) : base(context) { }

    public async Task<HashSet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> keys, CancellationToken ct = default)
    {
        var persisted = await _set.AsNoTracking()
            .Where(e => keys.Contains(e.IdempotencyKey))
            .Select(e => e.IdempotencyKey)
            .ToListAsync(ct);

        // Bút toán Added trong cùng scope chưa xuống DB — không tính thì hai lần gọi trước một SaveChanges
        // sẽ cùng lọt qua và vỡ ở unique index.
        var pending = _set.Local.Select(e => e.IdempotencyKey).Where(keys.Contains);
        return persisted.Concat(pending).ToHashSet();
    }

    public async Task<LedgerEntry?> GetByKeyAsync(string idempotencyKey, CancellationToken ct = default)
        => _set.Local.FirstOrDefault(e => e.IdempotencyKey == idempotencyKey)
           ?? await _set.AsNoTracking().FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey, ct);

    public async Task<Dictionary<Guid, GardenLedgerSummary>> GetGardenSummariesAsync(
        IReadOnlyCollection<Guid> gardenStoreIds, DateTime nowUtc, CancellationToken ct = default)
    {
        if (gardenStoreIds.Count == 0) return new Dictionary<Guid, GardenLedgerSummary>();

        var rows = await _set.AsNoTracking()
            .Where(e => e.Account == LedgerAccount.GardenStore && e.GardenStoreId != null
                        && gardenStoreIds.Contains(e.GardenStoreId.Value))
            .GroupBy(e => new
            {
                StoreId = e.GardenStoreId!.Value,
                IsAvailable = e.AvailableAt <= nowUtc,
                IsCommission = e.Type == LedgerEntryType.Commission
                               || e.Type == LedgerEntryType.CommissionReversal
                               || e.Type == LedgerEntryType.CommissionReinstated,
            })
            .Select(g => new { g.Key.StoreId, g.Key.IsAvailable, g.Key.IsCommission, Amount = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        return gardenStoreIds.Distinct().ToDictionary(id => id, id =>
        {
            var mine = rows.Where(r => r.StoreId == id).ToList();
            var available = mine.Where(r => r.IsAvailable).Sum(r => r.Amount);
            var pending = mine.Where(r => !r.IsAvailable).Sum(r => r.Amount);
            return new GardenLedgerSummary(available + pending, available, pending,
                -mine.Where(r => r.IsCommission).Sum(r => r.Amount));
        });
    }

    public async Task<GardenLedgerSummary> GetGardenSummaryAsync(Guid gardenStoreId, DateTime nowUtc, CancellationToken ct = default)
    {
        // Một lượt đi về DB: gộp theo (đã khả dụng?, là phí sàn?) rồi cộng trong C#.
        var rows = await _set.AsNoTracking()
            .Where(e => e.Account == LedgerAccount.GardenStore && e.GardenStoreId == gardenStoreId)
            .GroupBy(e => new
            {
                IsAvailable = e.AvailableAt <= nowUtc,
                IsCommission = e.Type == LedgerEntryType.Commission
                               || e.Type == LedgerEntryType.CommissionReversal
                               || e.Type == LedgerEntryType.CommissionReinstated,
            })
            .Select(g => new { g.Key.IsAvailable, g.Key.IsCommission, Amount = g.Sum(e => e.Amount) })
            .ToListAsync(ct);

        var available = rows.Where(r => r.IsAvailable).Sum(r => r.Amount);
        var pending = rows.Where(r => !r.IsAvailable).Sum(r => r.Amount);
        // Ở sổ nhà vườn phí sàn mang dấu âm — đổi dấu để báo cáo "đã thu bao nhiêu".
        var commission = -rows.Where(r => r.IsCommission).Sum(r => r.Amount);
        return new GardenLedgerSummary(available + pending, available, pending, commission);
    }
}
