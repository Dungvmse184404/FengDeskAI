using FengDeskAI.Domain.Entities.Payment;

namespace FengDeskAI.Application.Interfaces.Repositories;

/// <summary>Tổng hợp sổ của một nhà vườn tại một thời điểm.</summary>
/// <param name="Balance">Σ mọi bút toán — tiền sàn đang nợ nhà vườn.</param>
/// <param name="Available">Phần đã qua khoảng giữ (AvailableAt ≤ now) — có thể chi.</param>
/// <param name="Pending">Phần còn trong khoảng giữ.</param>
/// <param name="CommissionCharged">Σ phí sàn đã thu (sau khi trừ phần trả lại do hoàn hàng), số dương.</param>
public sealed record GardenLedgerSummary(decimal Balance, decimal Available, decimal Pending, decimal CommissionCharged);

public interface ILedgerRepository : IGenericRepository<LedgerEntry>
{
    /// <summary>Những khoá trong danh sách đã có bút toán (kể cả bút toán đang chờ SaveChanges trong scope này).</summary>
    Task<HashSet<string>> GetExistingKeysAsync(IReadOnlyCollection<string> keys, CancellationToken ct = default);

    /// <summary>Bút toán theo khoá — dùng khi cần đảo đúng số tiền đã ghi trước đó.</summary>
    Task<LedgerEntry?> GetByKeyAsync(string idempotencyKey, CancellationToken ct = default);

    Task<GardenLedgerSummary> GetGardenSummaryAsync(Guid gardenStoreId, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>Như <see cref="GetGardenSummaryAsync"/> cho nhiều vườn trong MỘT truy vấn. Vườn chưa có bút toán ⇒ số 0.</summary>
    Task<Dictionary<Guid, GardenLedgerSummary>> GetGardenSummariesAsync(
        IReadOnlyCollection<Guid> gardenStoreIds, DateTime nowUtc, CancellationToken ct = default);
}
